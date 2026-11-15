using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

/// <summary>Chứng minh cơ chế đổi ngưỡng nóng: engine đọc RiskOptions theo tham chiếu ở MỖI lần Observe.</summary>
public sealed class RiskOptionsHotReloadTests
{
    private static TrackedDetection Detection(double confidence = .95) => new("1", "drowning", confidence, [.1, .1, .3, .5]);

    [Fact]
    public void ChangingOptionsMidStream_ChangesBehaviorWithoutResettingState()
    {
        var options = new RiskOptions();
        var engine = new RiskDecisionEngine(options);
        // Mặc định MinObservations=3: 1 quan sát chưa đủ phát cảnh báo.
        Assert.Empty(engine.Observe(0, [Detection()]));
        Assert.Empty(engine.Observe(1, [Detection()]));

        // Đổi ngưỡng "nóng" trên instance đang dùng (bằng CopyFrom, như provider):
        options.CopyFrom(new RiskOptions { MinObservations = 1, WarningRatio = .9, CriticalRatio = .95, RecoveryRatio = 0,
            CriticalHoldSeconds = 0, RecoveryHoldSeconds = 0, CooldownSeconds = 0, WindowSeconds = 5, MaxObservationGapSeconds = 3 });
        // Cùng engine, cùng chuỗi quan sát: chỉ vì ngưỡng đổi nên cảnh báo xuất hiện (giữ nguyên state).
        var decision = Assert.Single(engine.Observe(2, [Detection()]));
        Assert.Equal("CRITICAL", decision.Severity);
        Assert.Equal(decision.Episode, Assert.Single(engine.Observe(3, [Detection()])).Episode);
    }

    [Fact]
    public void CopyFrom_ReplacesAllFields_AndIsValid()
    {
        var target = new RiskOptions { CooldownSeconds = 99 };
        target.CopyFrom(new RiskOptions { ConfidenceThreshold = .8, WarningRatio = .5, CriticalRatio = .9,
            RecoveryRatio = .1, CriticalHoldSeconds = 2, RecoveryHoldSeconds = 3, CooldownSeconds = 5,
            WindowSeconds = 4, MaxObservationGapSeconds = 2, MinObservations = 4 });
        Assert.Equal(.8, target.ConfidenceThreshold);
        Assert.Equal(5, target.CooldownSeconds);
        Assert.Equal(4, target.MinObservations);
        Assert.True(target.IsValid());
    }
}
