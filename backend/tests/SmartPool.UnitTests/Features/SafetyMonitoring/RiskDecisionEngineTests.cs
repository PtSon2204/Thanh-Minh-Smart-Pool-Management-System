using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class RiskDecisionEngineTests
{
    private static TrackedDetection Detection(string label = "drowning", double confidence = .9, string track = "1") =>
        new(track, label, confidence, [.1, .1, .3, .5]);

    [Fact]
    public void SingleFrameAndLowConfidence_DoNotTrigger()
    {
        var engine = new RiskDecisionEngine(new());
        Assert.Empty(engine.Observe(0, [Detection()]));
        for (var t = 1; t < 15; t++) Assert.Empty(engine.Observe(t, [Detection(confidence: .59)]));
        Assert.Empty(engine.Observe(15, []));
    }

    [Fact]
    public void SustainedRisk_EscalatesWithinSameEpisode_AfterHold()
    {
        var engine = new RiskDecisionEngine(new());
        engine.Observe(0, [Detection()]); engine.Observe(1, [Detection()]);
        var warning = Assert.Single(engine.Observe(2, [Detection()]));
        Assert.Equal("WARNING", warning.Severity);
        Assert.Equal("WARNING", Assert.Single(engine.Observe(3, [Detection()])).Severity);
        Assert.Equal("WARNING", Assert.Single(engine.Observe(4, [Detection()])).Severity);
        var critical = Assert.Single(engine.Observe(5, [Detection()]));
        Assert.Equal("CRITICAL", critical.Severity);
        Assert.Equal(warning.Episode, critical.Episode);
        Assert.Equal("CRITICAL", Assert.Single(engine.Observe(6, [Detection("normal")])).Severity);
    }

    [Fact]
    public void MissingTracking_DoesNotCountAsNormal_OrSatisfyCriticalHold()
    {
        var engine = new RiskDecisionEngine(new());
        for (var t = 0; t <= 2; t++) engine.Observe(t, [Detection()]);
        Assert.Empty(engine.Observe(3, []));
        Assert.Empty(engine.Observe(20, [Detection()]));
        Assert.Empty(engine.Observe(21, [Detection()]));
        Assert.Equal("WARNING", Assert.Single(engine.Observe(22, [Detection()])).Severity);
        Assert.Equal("CRITICAL", Assert.Single(engine.Observe(25, [Detection()])).Severity);
    }

    [Fact]
    public void RecoveryRequiresSustainedEvidence_ThenNewEpisodeAndCooldown()
    {
        var engine = new RiskDecisionEngine(new() { CooldownSeconds = 3 });
        RiskDecision? original = null;
        for (var t = 0; t <= 5; t++) original = engine.Observe(t, [Detection()]).SingleOrDefault() ?? original;
        for (var t = 6; t < 15; t++) Assert.Equal("CRITICAL", Assert.Single(engine.Observe(t, [Detection("normal")])).Severity);
        Assert.Empty(engine.Observe(15, [Detection("normal")]));
        Assert.Empty(engine.Observe(16, [Detection()]));
        Assert.Empty(engine.Observe(17, [Detection()]));
        var next = Assert.Single(engine.Observe(18, [Detection()]));
        Assert.Equal("WARNING", next.Severity);
        Assert.NotEqual(original!.Episode, next.Episode);
    }

    [Fact]
    public void TracksAndVideoTimestamps_AreIndependentOfCpuTime()
    {
        var engine = new RiskDecisionEngine(new());
        for (var t = 0; t <= 5; t++)
        {
            var result = engine.Observe(t, [Detection(track: "risk"), Detection("normal", track: "safe")]);
            Assert.DoesNotContain(result, x => x.TrackId == "safe");
            if (t == 5) Assert.Equal("CRITICAL", Assert.Single(result).Severity);
        }
        Assert.Empty(engine.Observe(5, [Detection()])); // Different track, one observation.
        Assert.Empty(engine.Observe(4, [Detection(track: "risk")])); // Out-of-order frame ignored.
    }
}
