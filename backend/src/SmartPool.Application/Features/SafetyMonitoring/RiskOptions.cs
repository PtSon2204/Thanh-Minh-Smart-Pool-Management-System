namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed class RiskOptions
{
    private readonly object sync = new();
    public double WindowSeconds { get; set; } = 5;
    public double ConfidenceThreshold { get; set; } = 0.60;
    public double WarningRatio { get; set; } = 0.40;
    public double CriticalRatio { get; set; } = 0.70;
    public double CriticalHoldSeconds { get; set; } = 3;
    public double RecoveryRatio { get; set; } = 0.20;
    public double RecoveryHoldSeconds { get; set; } = 5;
    public double MaxObservationGapSeconds { get; set; } = 3;
    public double CooldownSeconds { get; set; } = 15;
    public int MinObservations { get; set; } = 3;
    public bool IsValid() => WindowSeconds > 0 && ConfidenceThreshold is >= 0 and <= 1 &&
        RecoveryRatio >= 0 && RecoveryRatio < WarningRatio && WarningRatio < CriticalRatio && CriticalRatio <= 1 &&
        CriticalHoldSeconds > 0 && RecoveryHoldSeconds > 0 && MaxObservationGapSeconds > 0 &&
        CooldownSeconds >= 0 && MinObservations >= 2;

    public RiskOptions Clone()
    {
        lock (sync) return new()
        {
            WindowSeconds = WindowSeconds,
            ConfidenceThreshold = ConfidenceThreshold,
            WarningRatio = WarningRatio,
            CriticalRatio = CriticalRatio,
            CriticalHoldSeconds = CriticalHoldSeconds,
            RecoveryRatio = RecoveryRatio,
            RecoveryHoldSeconds = RecoveryHoldSeconds,
            MaxObservationGapSeconds = MaxObservationGapSeconds,
            CooldownSeconds = CooldownSeconds,
            MinObservations = MinObservations,
        };
    }

    // Ghi đè toàn bộ giá trị vào instance hiện tại. Provider dùng để "nóng" đổi ngưỡng
    // mà không tạo instance mới (giữ nguyên state theo dõi trong engine).
    public void CopyFrom(RiskOptions source)
    {
        var s = source.Clone();
        lock (sync)
        {
            WindowSeconds = s.WindowSeconds;
            ConfidenceThreshold = s.ConfidenceThreshold;
            WarningRatio = s.WarningRatio;
            CriticalRatio = s.CriticalRatio;
            CriticalHoldSeconds = s.CriticalHoldSeconds;
            RecoveryRatio = s.RecoveryRatio;
            RecoveryHoldSeconds = s.RecoveryHoldSeconds;
            MaxObservationGapSeconds = s.MaxObservationGapSeconds;
            CooldownSeconds = s.CooldownSeconds;
            MinObservations = s.MinObservations;
        }
    }
}
