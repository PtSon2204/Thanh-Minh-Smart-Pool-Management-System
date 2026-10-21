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
