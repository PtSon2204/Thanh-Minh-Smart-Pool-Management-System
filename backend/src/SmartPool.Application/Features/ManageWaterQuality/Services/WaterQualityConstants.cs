namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public static class WaterQualityConstants
{
    public const string EvaluationVersion = "v1.0-standard-vn";

    // Tiêu chuẩn lý tưởng bể bơi (tham chiếu CDC MAHC / APSP-11 & Thông tư 02/2011)
    public const decimal IdealPhMin = 7.20m;
    public const decimal IdealPhMax = 7.80m;
    public const decimal IdealChlorineMin = 1.00m;
    public const decimal IdealChlorineMax = 3.00m;

    // Ngưỡng cảnh báo (Warning thresholds)
    public const decimal WarningPhMin = 6.80m;
    public const decimal WarningPhMax = 8.20m;
    public const decimal WarningChlorineMin = 0.50m;
    public const decimal WarningChlorineMax = 5.00m;

    // Ngưỡng phân tích xu hướng đi xuống 3 ngày gần nhất (tham chiếu CDC MAHC / WHO / PHTA CPO)
    public const int TrendWindowDays = 3;
    public const decimal TrendPhMinCumulativeDelta = 0.20m;
    public const decimal TrendChlorineMinCumulativeDrop = 0.40m;
    public const decimal TrendChlorineLowMargin = 1.40m;
    public const decimal TrendChlorineLowBufferDrop = 0.25m;

    // Các trạng thái Status hợp lệ theo Database Check Constraint
    public const string StatusWithinLimits = "WithinLimits";
    public const string StatusWarning = "Warning";
    public const string StatusDanger = "Danger";
    public const string StatusIncomplete = "Incomplete";
    public const string StatusNotEvaluated = "NotEvaluated";
}
