using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

public sealed class IncidentStatisticsResponse
{
    // === 4 KPI CARDS ===
    public int TotalIncidents { get; set; }
    public string ComparisonWithLastMonth { get; set; } = "-35% so với tháng trước";

    public int ResolvedIncidents { get; set; }
    public double ResolvedPercentage { get; set; }

    public int InProgressIncidents { get; set; }

    public double AverageResolutionTimeHours { get; set; } // MTTR (Mean Time to Resolution)

    // === BIỂU ĐỒ CƠ CẤU PHÂN LOẠI (DONUT CHART) ===
    public List<IncidentCategoryDistributionDto> CategoryDistributions { get; set; } = new();

    // === BIỂU ĐỒ XU HƯỚNG SỰ CỐ THEO THÁNG (LINE CHART) ===
    public List<IncidentMonthlyTrendDto> MonthlyTrends { get; set; } = new();

    // === NHẬT KÝ CÁC SỰ CỐ GHI NHẬN GẦN NHẤT (TABLE) ===
    public List<RecentIncidentDto> RecentIncidents { get; set; } = new();
}
