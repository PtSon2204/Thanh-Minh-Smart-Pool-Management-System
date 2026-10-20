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

public sealed class IncidentCategoryDistributionDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class IncidentMonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
}

public sealed class RecentIncidentDto
{
    public Guid Id { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public string ReportedTime { get; set; } = string.Empty;
    public string HandlerStaffName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResolutionTime { get; set; } = string.Empty;
}
