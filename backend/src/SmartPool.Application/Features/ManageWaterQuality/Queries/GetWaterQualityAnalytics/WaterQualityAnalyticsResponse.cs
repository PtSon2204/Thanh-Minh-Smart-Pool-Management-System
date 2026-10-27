using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;

public sealed class WaterQualityAnalyticsResponse
{
    public int TotalLogs { get; set; }
    public int WithinLimitsCount { get; set; }
    public int WarningCount { get; set; }
    public int DangerCount { get; set; }
    public double ComplianceRatePercentage { get; set; }

    public decimal? AvgPh { get; set; }
    public decimal? MinPh { get; set; }
    public decimal? MaxPh { get; set; }

    public decimal? AvgChlorine { get; set; }
    public decimal? MinChlorine { get; set; }
    public decimal? MaxChlorine { get; set; }

    public List<WaterQualityTrendPoint> DailyTrends { get; set; } = new();
    public List<WaterQualityLogDto> RecentAlerts { get; set; } = new();
}

public sealed class WaterQualityTrendPoint
{
    public string Date { get; set; } = string.Empty;
    public decimal AvgPh { get; set; }
    public decimal AvgChlorine { get; set; }
    public int TotalMeasurements { get; set; }
    public int WithinLimitsCount { get; set; }
    public int WarningCount { get; set; }
    public int DangerCount { get; set; }
}
