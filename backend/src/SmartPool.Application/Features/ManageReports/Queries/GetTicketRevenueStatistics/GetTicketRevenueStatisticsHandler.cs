using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;

public sealed class GetTicketRevenueStatisticsHandler : IRequestHandler<GetTicketRevenueStatisticsQuery, TicketRevenueStatisticsResponse>
{
    private readonly IReportOperations _reportOps;

    public GetTicketRevenueStatisticsHandler(IReportOperations reportOps)
    {
        _reportOps = reportOps;
    }

    public async Task<TicketRevenueStatisticsResponse> Handle(
        GetTicketRevenueStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (fromDate, toDate, daysCount) = ResolveDateRange(request, now);

        var categoryFilter = request.TicketCategory?.Trim().ToUpperInvariant() ?? "ALL";
        bool isAll = categoryFilter == "ALL" || string.IsNullOrEmpty(categoryFilter);
        bool isSingle = !isAll && (categoryFilter == "VE_LUOT" || categoryFilter == "SINGLE" || categoryFilter == "LUOT");
        bool isMonthly = !isAll && (categoryFilter == "VE_THANG" || categoryFilter == "MONTHLY" || categoryFilter == "THANG");

        // Lấy dữ liệu từ DB
        var allOrders = await _reportOps.GetOrdersAsync(cancellationToken);
        var allOrderDetails = await _reportOps.GetOrderDetailsAsync(cancellationToken);
        var allPayments = await _reportOps.GetPaymentsAsync(cancellationToken);
        var allTickets = await _reportOps.GetTicketsAsync(cancellationToken);
        var allTicketTypes = (await _reportOps.GetTicketTypesAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var allUsers = (await _reportOps.GetUsersAsync(cancellationToken)).ToDictionary(u => u.Id, u => u);
        var allProfiles = (await _reportOps.GetUserProfilesAsync(cancellationToken)).ToDictionary(p => p.UserId, p => p);

        bool MatchesCategory(TicketType? tt)
        {
            if (isAll) return true;
            if (tt == null) return false;
            var cat = tt.TicketCategory?.Trim().ToUpperInvariant() ?? "";
            var name = tt.Name?.ToLowerInvariant() ?? "";
            if (isSingle)
                return cat == "VE_LUOT" || cat == "SINGLE" || name.Contains("lượt") || name.Contains("ngày") || name.Contains("ngay");
            if (isMonthly)
                return cat == "VE_THANG" || cat == "MONTHLY" || name.Contains("tháng") || name.Contains("thang");
            return true;
        }

        // 1. Lọc orders trong khoảng thời gian & có trạng thái hợp lệ (không bị xóa)
        var dateFilteredOrders = allOrders
            .Where(o => o.IsDeleted != true && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .ToList();
        var dateOrderIds = dateFilteredOrders.Select(o => o.Id).ToHashSet();

        // 2. OrderDetails liên quan đến ticket trong các order hợp lệ & khớp category
        var ticketDetails = allOrderDetails
            .Where(od => dateOrderIds.Contains(od.OrderId) &&
                         od.ItemType.Equals("Ticket", StringComparison.OrdinalIgnoreCase) &&
                         (!allTicketTypes.TryGetValue(od.ItemId, out var tt) || MatchesCategory(tt)))
            .ToList();

        var matchedOrderIds = ticketDetails.Select(od => od.OrderId).ToHashSet();

        var filteredOrders = isAll
            ? dateFilteredOrders
            : dateFilteredOrders.Where(o => matchedOrderIds.Contains(o.Id)).ToList();

        // Lọc orders kỳ trước để tính growth rate
        var prevDays = (toDate - fromDate).Days;
        var prevFrom = fromDate.AddDays(-prevDays);
        var prevDateOrders = allOrders
            .Where(o => o.IsDeleted != true && o.CreatedAt >= prevFrom && o.CreatedAt < fromDate)
            .ToList();
        var prevDateOrderIds = prevDateOrders.Select(o => o.Id).ToHashSet();

        var prevTicketDetails = allOrderDetails
            .Where(od => prevDateOrderIds.Contains(od.OrderId) &&
                         od.ItemType.Equals("Ticket", StringComparison.OrdinalIgnoreCase) &&
                         (!allTicketTypes.TryGetValue(od.ItemId, out var tt) || MatchesCategory(tt)))
            .ToList();
        var prevMatchedOrderIds = prevTicketDetails.Select(od => od.OrderId).ToHashSet();

        var prevOrders = isAll
            ? prevDateOrders
            : prevDateOrders.Where(o => prevMatchedOrderIds.Contains(o.Id)).ToList();

        // === 1. KPI CARDS ===
        int totalTicketsSold = ticketDetails.Sum(od => od.Quantity);
        decimal totalRevenue = isAll
            ? filteredOrders.Sum(o => o.FinalAmount)
            : ticketDetails.Sum(od => od.UnitPrice * od.Quantity);
        decimal avgOrderValue = filteredOrders.Count > 0 ? totalRevenue / filteredOrders.Count : 0;

        decimal prevRevenue = isAll
            ? prevOrders.Sum(o => o.FinalAmount)
            : prevTicketDetails.Sum(od => od.UnitPrice * od.Quantity);

        double growthRate = prevRevenue > 0
            ? (double)((totalRevenue - prevRevenue) / prevRevenue * 100)
            : (totalRevenue > 0 ? 100 : 0);

        // === 2. DAILY REVENUE (Area Chart) ===
        var dailyRevenues = new List<DailyRevenueDto>();
        for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
        {
            var dayOrders = filteredOrders.Where(o => o.CreatedAt?.Date == day).ToList();
            var dayOrderIds = dayOrders.Select(o => o.Id).ToHashSet();
            var dayTicketDetails = ticketDetails.Where(od => dayOrderIds.Contains(od.OrderId)).ToList();

            dailyRevenues.Add(new DailyRevenueDto
            {
                Date = day.ToString("dd/MM"),
                Revenue = isAll ? dayOrders.Sum(o => o.FinalAmount) : dayTicketDetails.Sum(od => od.UnitPrice * od.Quantity),
                TicketCount = dayTicketDetails.Sum(od => od.Quantity)
            });
        }

        // === 3. TICKET TYPE SALES (Pie Chart) ===
        var ticketTypeSalesColors = new[] { "#0284c7", "#8b5cf6", "#f59e0b", "#10b981", "#ef4444", "#ec4899", "#6366f1" };
        var ticketsByType = allTickets
            .Where(t => t.IsDeleted != true && t.CreatedAt >= fromDate && t.CreatedAt <= toDate &&
                        allTicketTypes.TryGetValue(t.TicketTypeId, out var tt) && MatchesCategory(tt))
            .GroupBy(t => t.TicketTypeId)
            .Select((g, idx) =>
            {
                var typeName = allTicketTypes.TryGetValue(g.Key, out var tt) ? tt.Name : "Khác";
                return new TicketTypeSalesDto
                {
                    Name = typeName,
                    Value = g.Count(),
                    Color = ticketTypeSalesColors[idx % ticketTypeSalesColors.Length]
                };
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        // === 4. TICKET TYPE REVENUE (Bar Chart) ===
        var ticketTypeRevenues = ticketDetails
            .GroupBy(od => od.ItemId)
            .Select(g =>
            {
                var typeName = allTicketTypes.TryGetValue(g.Key, out var tt) ? tt.Name : "Khác";
                return new TicketTypeRevenueDto
                {
                    Name = typeName,
                    Revenue = g.Sum(od => od.UnitPrice * od.Quantity),
                    Count = g.Sum(od => od.Quantity)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Nếu không có data thực, nhóm theo TicketType từ tickets
        if (!ticketTypeRevenues.Any() && ticketsByType.Any())
        {
            ticketTypeRevenues = ticketsByType.Select(ts =>
            {
                var tt = allTicketTypes.Values.FirstOrDefault(t => t.Name == ts.Name);
                return new TicketTypeRevenueDto
                {
                    Name = ts.Name,
                    Revenue = tt != null ? tt.Price * ts.Value : 0,
                    Count = ts.Value
                };
            }).ToList();
        }

        // === 5. RECENT TRANSACTIONS (Table) ===
        var paymentsByOrder = allPayments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PaymentTime).First());

        var recentTransactions = filteredOrders
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .Select(o =>
            {
                var orderDetails = allOrderDetails.Where(od => od.OrderId == o.Id).ToList();
                var ticketDetail = orderDetails.FirstOrDefault(od =>
                    od.ItemType.Equals("Ticket", StringComparison.OrdinalIgnoreCase) &&
                    allTicketTypes.TryGetValue(od.ItemId, out var tt) && MatchesCategory(tt))
                    ?? orderDetails.FirstOrDefault(od => od.ItemType.Equals("Ticket", StringComparison.OrdinalIgnoreCase));

                string ticketTypeName = "—";
                if (ticketDetail != null && allTicketTypes.TryGetValue(ticketDetail.ItemId, out var tt))
                    ticketTypeName = tt.Name;

                string customerName = o.CustomerName ?? "Khách vãng lai";
                if (o.UserId.HasValue)
                {
                    if (allProfiles.TryGetValue(o.UserId.Value, out var profile))
                        customerName = profile.FullName ?? customerName;
                    else if (allUsers.TryGetValue(o.UserId.Value, out var user))
                        customerName = user.Username ?? customerName;
                }

                string paymentMethod = "—";
                if (paymentsByOrder.TryGetValue(o.Id, out var payment))
                    paymentMethod = payment.PaymentMethod;

                return new RecentTransactionDto
                {
                    Id = o.Id,
                    CustomerName = customerName,
                    Phone = o.CustomerPhone,
                    TicketType = ticketTypeName,
                    Quantity = orderDetails.Sum(od => od.Quantity),
                    TotalAmount = isAll ? o.FinalAmount : orderDetails.Where(od => allTicketTypes.TryGetValue(od.ItemId, out var tt) && MatchesCategory(tt)).Sum(od => od.UnitPrice * od.Quantity),
                    PaymentMethod = paymentMethod,
                    Status = o.Status ?? "Pending",
                    CreatedAt = o.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "—"
                };
            })
            .Where(t => isAll || t.TotalAmount > 0)
            .ToList();

        return new TicketRevenueStatisticsResponse
        {
            TotalTicketsSold = totalTicketsSold,
            TotalRevenue = totalRevenue,
            AverageOrderValue = Math.Round(avgOrderValue, 0),
            RevenueGrowthRate = Math.Round(growthRate, 1),
            DailyRevenues = dailyRevenues,
            TicketTypeSales = ticketsByType,
            TicketTypeRevenues = ticketTypeRevenues,
            RecentTransactions = recentTransactions
        };
    }

    private static (DateTime fromDate, DateTime toDate, int daysCount) ResolveDateRange(
        GetTicketRevenueStatisticsQuery request, DateTime now)
    {
        return request.Period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now, 1),
            "7days" => (now.AddDays(-7).Date, now, 7),
            "30days" => (now.AddDays(-30).Date, now, 30),
            "month" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now,
                        DateTime.DaysInMonth(now.Year, now.Month)),
            "custom" when request.FromDate.HasValue && request.ToDate.HasValue
                => (request.FromDate.Value.Date, request.ToDate.Value.Date.AddDays(1).AddTicks(-1),
                    (request.ToDate.Value.Date - request.FromDate.Value.Date).Days + 1),
            _ => (now.AddDays(-7).Date, now, 7)
        };
    }

}
