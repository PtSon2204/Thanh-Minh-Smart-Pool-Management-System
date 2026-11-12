using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageReports.Queries.GetRentalStatistics;

/// <summary>
/// Thống kê chi phí / hoạt động thuê đồ. Period: today, 7days, 30days, month, custom.
/// </summary>
public sealed record GetRentalStatisticsQuery : IRequest<RentalStatisticsResponse>
{
    public string Period { get; init; } = "7days";
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class RentalStatisticsResponse
{
    public int TotalRentals { get; set; }
    public int ActiveRentals { get; set; }
    public int ReturnedRentals { get; set; }
    public int DamagedOrLostRentals { get; set; }
    public decimal TotalFee { get; set; }
    public decimal DepositHeld { get; set; }
    public decimal RetainedAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public List<RentalDailyDto> DailyStats { get; set; } = new();
    public List<RentalStatusDto> StatusBreakdown { get; set; } = new();
    public List<RentalTopProductDto> TopProducts { get; set; } = new();
    public List<RentalRecentDto> RecentRentals { get; set; } = new();
}

public sealed class RentalDailyDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Fee { get; set; }
}

public sealed class RentalStatusDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class RentalTopProductDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Fee { get; set; }
}

public sealed class RentalRecentDto
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string RentTime { get; set; } = string.Empty;
    public string ReturnTime { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public decimal Deposit { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class GetRentalStatisticsHandler : IRequestHandler<GetRentalStatisticsQuery, RentalStatisticsResponse>
{
    private readonly IReportOperations _reportOps;

    public GetRentalStatisticsHandler(IReportOperations reportOps)
    {
        _reportOps = reportOps;
    }

    public async Task<RentalStatisticsResponse> Handle(GetRentalStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (from, to) = Resolve(request.Period, request.FromDate, request.ToDate, now);

        var products = (await _reportOps.GetProductsAsync(cancellationToken)).ToDictionary(p => p.Id, p => p);
        var rentals = (await _reportOps.GetRentalsAsync(cancellationToken))
            .Where(r => r.RentTime.HasValue && r.RentTime.Value >= from && r.RentTime.Value <= to)
            .ToList();
        var txs = (await _reportOps.GetRentalTransactionsAsync(cancellationToken))
            .Where(t => t.ConfirmedAtUtc >= from && t.ConfirmedAtUtc <= to)
            .ToList();

        string NameOf(Rental r) => products.TryGetValue(r.ProductId, out var p) ? p.Name : (r.ProductNameSnapshot ?? "Đồ cho thuê");
        static string Status(Rental r) => string.IsNullOrWhiteSpace(r.Status) ? "Renting" : r.Status!;

        var daily = new List<RentalDailyDto>();
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var items = rentals.Where(r => r.RentTime!.Value.Date == day).ToList();
            daily.Add(new RentalDailyDto { Date = day.ToString("dd/MM"), Count = items.Count, Fee = items.Sum(r => r.FeeAmount ?? 0m) });
        }

        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Renting"] = "#0284c7", ["Returned"] = "#10b981", ["Resolved"] = "#10b981",
            ["Completed"] = "#10b981", ["Damaged"] = "#f59e0b", ["Lost"] = "#ef4444"
        };

        return new RentalStatisticsResponse
        {
            TotalRentals = rentals.Count,
            ActiveRentals = rentals.Count(r => Status(r).Equals("Renting", StringComparison.OrdinalIgnoreCase)),
            ReturnedRentals = rentals.Count(r => Status(r) is "Returned" or "Resolved" or "Completed"),
            DamagedOrLostRentals = rentals.Count(r => Status(r) is "Damaged" or "Lost"),
            TotalFee = rentals.Sum(r => r.FeeAmount ?? 0m),
            DepositHeld = rentals.Where(r => Status(r).Equals("Renting", StringComparison.OrdinalIgnoreCase)).Sum(r => r.DepositAmount ?? 0m),
            RetainedAmount = txs.Sum(t => t.RetainedAmount),
            RefundedAmount = txs.Sum(t => t.RefundAmount),
            DailyStats = daily,
            StatusBreakdown = rentals
                .GroupBy(Status)
                .Select(g => new RentalStatusDto { Name = g.Key, Value = g.Count(), Color = colors.TryGetValue(g.Key, out var c) ? c : "#94a3b8" })
                .OrderByDescending(x => x.Value)
                .ToList(),
            TopProducts = rentals
                .GroupBy(r => r.ProductId)
                .Select(g => new RentalTopProductDto { Name = NameOf(g.First()), Count = g.Count(), Fee = g.Sum(r => r.FeeAmount ?? 0m) })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToList(),
            RecentRentals = rentals
                .OrderByDescending(r => r.RentTime)
                .Take(20)
                .Select(r => new RentalRecentDto
                {
                    Id = r.Id,
                    ProductName = NameOf(r),
                    RentTime = r.RentTime!.Value.ToString("dd/MM/yyyy HH:mm"),
                    ReturnTime = r.ReturnTime?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                    Fee = r.FeeAmount ?? 0m,
                    Deposit = r.DepositAmount ?? 0m,
                    Status = Status(r)
                })
                .ToList()
        };
    }

    private static (DateTime From, DateTime To) Resolve(string? period, DateTime? fromDate, DateTime? toDate, DateTime now)
    {
        return period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now),
            "7days" => (now.AddDays(-7).Date, now),
            "30days" => (now.AddDays(-30).Date, now),
            "month" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now),
            "custom" when fromDate.HasValue && toDate.HasValue
                => (fromDate.Value.Date, toDate.Value.Date.AddDays(1).AddTicks(-1)),
            _ => (now.AddDays(-7).Date, now)
        };
    }
}
