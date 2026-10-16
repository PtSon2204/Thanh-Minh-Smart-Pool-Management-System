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

public sealed class DailyRevenueDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TicketCount { get; set; }
}

public sealed class TicketTypeSalesDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class TicketTypeRevenueDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Count { get; set; }
}

public sealed class RecentTransactionDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
