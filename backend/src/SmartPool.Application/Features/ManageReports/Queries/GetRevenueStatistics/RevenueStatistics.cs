using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageReports.Queries.GetRevenueStatistics;

/// <summary>
/// Thống kê doanh thu tổng hợp từ mọi nguồn (vé, cho thuê đồ).
/// Period: today, 7days, 30days, month, custom.
/// </summary>
public sealed record GetRevenueStatisticsQuery : IRequest<RevenueStatisticsResponse>
{
    public string Period { get; init; } = "7days";
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class RevenueStatisticsResponse
{
    public decimal TotalRevenue { get; set; }
    public decimal TicketRevenue { get; set; }
    public decimal RentalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public double RevenueGrowthRate { get; set; }
    public List<DailyRevenueBySourceDto> DailyRevenues { get; set; } = new();
    public List<RevenueSourceDto> RevenueBySource { get; set; } = new();
    public List<RevenueItemDto> TopItems { get; set; } = new();
}

public sealed class DailyRevenueBySourceDto
{
    public string Date { get; set; } = string.Empty;
    public decimal TicketRevenue { get; set; }
    public decimal RentalRevenue { get; set; }
    public decimal Total { get; set; }
}

public sealed class RevenueSourceDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class RevenueItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class GetRevenueStatisticsHandler : IRequestHandler<GetRevenueStatisticsQuery, RevenueStatisticsResponse>
{
    private readonly IReportOperations _reportOps;

    public GetRevenueStatisticsHandler(IReportOperations reportOps)
    {
        _reportOps = reportOps;
    }

    public async Task<RevenueStatisticsResponse> Handle(GetRevenueStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (from, to) = ReportDateRange.Resolve(request.Period, request.FromDate, request.ToDate, now);
        var span = (to.Date - from.Date).Days + 1;
        var prevFrom = from.AddDays(-span);
        var prevTo = from.AddTicks(-1);

        var orders = (await _reportOps.GetOrdersAsync(cancellationToken))
            .Where(o => o.IsDeleted != true && !string.Equals(o.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var details = await _reportOps.GetOrderDetailsAsync(cancellationToken);
        var ticketTypes = (await _reportOps.GetTicketTypesAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var rentals = await _reportOps.GetRentalsAsync(cancellationToken);
        var rentalTxs = await _reportOps.GetRentalTransactionsAsync(cancellationToken);
        var products = (await _reportOps.GetProductsAsync(cancellationToken)).ToDictionary(p => p.Id, p => p);

        var orderDates = orders.ToDictionary(o => o.Id, o => o.CreatedAt);

        List<(DateTime Date, OrderDetail Detail)> TicketLines(DateTime a, DateTime b) => details
            .Where(d => string.Equals(d.ItemType, "TICKET", StringComparison.OrdinalIgnoreCase)
                        && orderDates.TryGetValue(d.OrderId, out var dt) && dt.HasValue && dt.Value >= a && dt.Value <= b)
            .Select(d => (orderDates[d.OrderId]!.Value, d))
            .ToList();

        List<(DateTime Date, decimal Amount)> RentalLines(DateTime a, DateTime b)
        {
            var fees = rentals
                .Where(r => r.RentTime.HasValue && r.RentTime.Value >= a && r.RentTime.Value <= b)
                .Select(r => (r.RentTime!.Value, r.FeeAmount ?? 0m));
            var retained = rentalTxs
                .Where(t => t.RetainedAmount > 0 && t.ConfirmedAtUtc >= a && t.ConfirmedAtUtc <= b)
                .Select(t => (t.ConfirmedAtUtc, t.RetainedAmount));
            return fees.Concat(retained).ToList();
        }

        var ticketLines = TicketLines(from, to);
        var rentalLines = RentalLines(from, to);

        var ticketRevenue = ticketLines.Sum(x => x.Detail.UnitPrice * x.Detail.Quantity);
        var rentalRevenue = rentalLines.Sum(x => x.Amount);
        var totalRevenue = ticketRevenue + rentalRevenue;

        var prevRevenue = TicketLines(prevFrom, prevTo).Sum(x => x.Detail.UnitPrice * x.Detail.Quantity)
                          + RentalLines(prevFrom, prevTo).Sum(x => x.Amount);
        var growth = prevRevenue > 0
            ? (double)((totalRevenue - prevRevenue) / prevRevenue * 100)
            : (totalRevenue > 0 ? 100 : 0);

        var daily = new List<DailyRevenueBySourceDto>();
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var t = ticketLines.Where(x => x.Date.Date == day).Sum(x => x.Detail.UnitPrice * x.Detail.Quantity);
            var r = rentalLines.Where(x => x.Date.Date == day).Sum(x => x.Amount);
            daily.Add(new DailyRevenueBySourceDto { Date = day.ToString("dd/MM"), TicketRevenue = t, RentalRevenue = r, Total = t + r });
        }

        var topItems = ticketLines
            .GroupBy(x => x.Detail.ItemId)
            .Select(g => new RevenueItemDto
            {
                Name = ticketTypes.TryGetValue(g.Key, out var tt) ? tt.Name : "Vé khác",
                Source = "Vé",
                Quantity = g.Sum(x => x.Detail.Quantity),
                Revenue = g.Sum(x => x.Detail.UnitPrice * x.Detail.Quantity)
            })
            .Concat(rentals
                .Where(r => r.RentTime.HasValue && r.RentTime.Value >= from && r.RentTime.Value <= to)
                .GroupBy(r => r.ProductId)
                .Select(g => new RevenueItemDto
                {
                    Name = products.TryGetValue(g.Key, out var p) ? p.Name : (g.First().ProductNameSnapshot ?? "Đồ cho thuê"),
                    Source = "Thuê đồ",
                    Quantity = g.Count(),
                    Revenue = g.Sum(r => r.FeeAmount ?? 0m)
                }))
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToList();

        return new RevenueStatisticsResponse
        {
            TotalRevenue = totalRevenue,
            TicketRevenue = ticketRevenue,
            RentalRevenue = rentalRevenue,
            TotalTransactions = ticketLines.Select(x => x.Detail.OrderId).Distinct().Count() + rentals.Count(r => r.RentTime.HasValue && r.RentTime.Value >= from && r.RentTime.Value <= to),
            RevenueGrowthRate = Math.Round(growth, 1),
            DailyRevenues = daily,
            RevenueBySource = new List<RevenueSourceDto>
            {
                new() { Name = "Bán vé", Value = ticketRevenue, Color = "#0284c7" },
                new() { Name = "Cho thuê đồ", Value = rentalRevenue, Color = "#f59e0b" }
            },
            TopItems = topItems
        };
    }
}

internal static class ReportDateRange
{
    public static (DateTime From, DateTime To) Resolve(string? period, DateTime? fromDate, DateTime? toDate, DateTime now)
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
