namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public static class WaterQualityConstants
{
    public const string EvaluationVersion = "v1.0-standard-vn";

    // Tiêu chuẩn lý tưởng bể bơi
    public const decimal IdealPhMin = 7.20m;
    public const decimal IdealPhMax = 7.80m;
    public const decimal IdealChlorineMin = 1.00m;
    public const decimal IdealChlorineMax = 3.00m;

    // Ngưỡng cảnh báo (Warning thresholds)
    public const decimal WarningPhMin = 6.80m;
    public const decimal WarningPhMax = 8.20m;
    public const decimal WarningChlorineMin = 0.50m;
    public const decimal WarningChlorineMax = 5.00m;

    // Các trạng thái Status hợp lệ theo Database Check Constraint
    public const string StatusWithinLimits = "WithinLimits";
    public const string StatusWarning = "Warning";
    public const string StatusDanger = "Danger";
    public const string StatusIncomplete = "Incomplete";
    public const string StatusNotEvaluated = "NotEvaluated";
}
