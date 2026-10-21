using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;

public sealed class TicketRevenueStatisticsResponse
{
    // 1. KPI Cards
    public int TotalTicketsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public double RevenueGrowthRate { get; set; }

    // 2. Doanh thu theo ngày (Area chart)
    public List<DailyRevenueDto> DailyRevenues { get; set; } = new();

    // 3. Số vé bán theo loại (Pie chart)
    public List<TicketTypeSalesDto> TicketTypeSales { get; set; } = new();

    // 4. Doanh thu theo loại vé (Bar chart)
    public List<TicketTypeRevenueDto> TicketTypeRevenues { get; set; } = new();

    // 5. Bảng chi tiết giao dịch gần nhất (Table)
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}
