using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class GetCustomerStatisticsHandler : IRequestHandler<GetCustomerStatisticsQuery, CustomerStatisticsResponse>
{
    private readonly IReportOperations _reportOps;

    public GetCustomerStatisticsHandler(IReportOperations reportOps)
    {
        _reportOps = reportOps;
    }

    public async Task<CustomerStatisticsResponse> Handle(GetCustomerStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (fromDate, toDate, daysCount) = ResolveDateRange(request, now);

        // Lấy dữ liệu cơ bản từ database
        var allUsers = await _reportOps.GetUsersAsync(cancellationToken);
        var allProfiles = (await _reportOps.GetUserProfilesAsync(cancellationToken)).ToDictionary(p => p.UserId, p => p);
        var allTickets = await _reportOps.GetTicketsAsync(cancellationToken);
        var allTicketTypes = (await _reportOps.GetTicketTypesAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var allEntries = await _reportOps.GetEntryLogsAsync(cancellationToken);
        var allOrders = await _reportOps.GetOrdersAsync(cancellationToken);

        // Lọc người dùng khách hàng (không tính admin hệ thống nếu có)
        var customerUsers = allUsers.Where(u => u.IsDeleted != true).ToList();

        // 1. KPI Cards
        var totalCustomers = customerUsers.Count;
        var newCustomersInPeriod = customerUsers.Count(u => u.CreatedAt.HasValue && u.CreatedAt.Value >= fromDate && u.CreatedAt.Value <= toDate);
        
        var activeMonthly = allTickets.Count(t => 
            t.IsDeleted != true && 
            t.ExpiryDate.HasValue && t.ExpiryDate.Value >= now &&
            allTicketTypes.TryGetValue(t.TicketTypeId, out var tt) && (tt.DurationDays ?? 0) >= 30);

        var periodEntries = allEntries.Where(e => e.ScanTime.HasValue && e.ScanTime.Value >= fromDate && e.ScanTime.Value <= toDate).ToList();
        var totalSwimEntries = periodEntries.Count;

        // 2. Xu hướng theo ngày (Daily Trends)
        var dailyTrends = new List<CustomerDailyTrendDto>();
        for (var i = 0; i < daysCount; i++)
        {
            var dayStart = fromDate.AddDays(i).Date;
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);
            var dateStr = dayStart.ToString("dd/MM");

            var newUsersOnDay = customerUsers.Count(u => u.CreatedAt.HasValue && u.CreatedAt.Value.Date == dayStart);
            var entriesOnDay = allEntries.Count(e => e.ScanTime.HasValue && e.ScanTime.Value.Date == dayStart);

            dailyTrends.Add(new CustomerDailyTrendDto
            {
                Date = dateStr,
                NewUsers = newUsersOnDay,
                SwimEntries = entriesOnDay
            });
        }

        // 3. Phân bổ theo khung giờ (Hourly Distribution từ 05:00 đến 21:00)
        var hourlyDistribution = new List<HourlyEntryDistributionDto>();
        for (var hour = 5; hour <= 21; hour++)
        {
            var count = periodEntries.Count(e => e.ScanTime.HasValue && e.ScanTime.Value.Hour == hour);
            hourlyDistribution.Add(new HourlyEntryDistributionDto
            {
                Hour = $"{hour:D2}:00",
                Count = count
            });
        }

        // 4. Phân khúc khách hàng (Segments)
        var dayTicketsCount = allTickets.Count(t => allTicketTypes.TryGetValue(t.TicketTypeId, out var tt) && (tt.DurationDays ?? 0) < 30);
        var monthTicketsCount = allTickets.Count(t => allTicketTypes.TryGetValue(t.TicketTypeId, out var tt) && (tt.DurationDays ?? 0) >= 30);

        var segments = new List<CustomerSegmentDto>
        {
            new() { Name = "Vé lượt (Vãng lai)", Value = dayTicketsCount, Color = "#0284c7" },
            new() { Name = "Hội viên vé tháng", Value = monthTicketsCount, Color = "#8b5cf6" },
        };

        // 5. Top khách hàng tiêu biểu
        var topCustomers = new List<TopCustomerDto>();
        foreach (var u in customerUsers.Take(10))
        {
            allProfiles.TryGetValue(u.Id, out var profile);
            var userTickets = allTickets.Where(t => t.UserId == u.Id).ToList();
            var userEntries = allEntries.Count(e => userTickets.Any(ut => ut.Id == e.TicketId));
            var userOrders = allOrders.Where(o => o.UserId == u.Id).ToList();
            var totalSpent = userOrders.Sum(o => o.TotalAmount);

            var ticketTypeDesc = userTickets.Any(t => allTicketTypes.TryGetValue(t.TicketTypeId, out var tt) && (tt.DurationDays ?? 0) >= 30)
                ? "Hội viên tháng"
                : "Vé lượt";

            topCustomers.Add(new TopCustomerDto
            {
                Id = u.Id,
                FullName = profile?.FullName ?? u.Username ?? "Khách hàng",
                Email = u.Email,
                Phone = u.Phone,
                TicketType = ticketTypeDesc,
                TotalEntries = userEntries,
                TotalSpending = totalSpent,
                LastVisit = u.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "—"
            });
        }

        return new CustomerStatisticsResponse
        {
            TotalCustomers = totalCustomers,
            NewCustomersInPeriod = newCustomersInPeriod,
            ActiveMonthlySubscribers = activeMonthly,
            TotalSwimEntries = totalSwimEntries,
            GrowthRate = newCustomersInPeriod > 0 ? Math.Round((double)newCustomersInPeriod / Math.Max(1, totalCustomers) * 100, 1) : 0,
            DailyTrends = dailyTrends,
            HourlyDistribution = hourlyDistribution,
            Segments = segments,
            TopCustomers = topCustomers
        };
    }

    private static (DateTime fromDate, DateTime toDate, int daysCount) ResolveDateRange(GetCustomerStatisticsQuery request, DateTime now)
    {
        return request.Period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now.Date.AddDays(1).AddTicks(-1), 1),
            "month" => (new DateTime(now.Year, now.Month, 1), now, DateTime.DaysInMonth(now.Year, now.Month)),
            "30days" => (now.Date.AddDays(-29), now, 30),
            "custom" when request.FromDate.HasValue && request.ToDate.HasValue => 
                (request.FromDate.Value, request.ToDate.Value, Math.Max(1, (int)(request.ToDate.Value - request.FromDate.Value).TotalDays + 1)),
            _ => (now.Date.AddDays(-6), now, 7) // mặc định 7 ngày
        };
    }

}
