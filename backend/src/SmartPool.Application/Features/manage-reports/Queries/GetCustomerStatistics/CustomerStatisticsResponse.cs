using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class CustomerStatisticsResponse
{
    // 1. KPI Cards
    public int TotalCustomers { get; set; }
    public int NewCustomersInPeriod { get; set; }
    public int ActiveMonthlySubscribers { get; set; }
    public int TotalSwimEntries { get; set; }
    public double GrowthRate { get; set; }

    // 2. Trend theo ngày (Line/Area chart)
    public List<CustomerDailyTrendDto> DailyTrends { get; set; } = new();

    // 3. Phân bổ theo khung giờ trong ngày (Bar chart)
    public List<HourlyEntryDistributionDto> HourlyDistribution { get; set; } = new();

    // 4. Phân khúc khách hàng (Pie/Donut chart)
    public List<CustomerSegmentDto> Segments { get; set; } = new();

    // 5. Bảng xếp hạng Top khách hàng (Table)
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
}

public sealed class CustomerDailyTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int NewUsers { get; set; }
    public int SwimEntries { get; set; }
}

public sealed class HourlyEntryDistributionDto
{
    public string Hour { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class CustomerSegmentDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class TopCustomerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string TicketType { get; set; } = "Vé lượt";
    public int TotalEntries { get; set; }
    public decimal TotalSpending { get; set; }
    public string LastVisit { get; set; } = string.Empty;
}
