using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class LiveRiskOverlayTests
{
    private static TrackedDetection Detection(double confidence = .9, double left = .1) => new("person", "drowning", confidence, [left, .1, left + .2, .5]);

    [Fact]
    public void ConfidenceDip_KeepsPairedPixelsAndNewViewerMetadata_WithoutCreatingRiskEvidence()
    {
        var engine = new RiskDecisionEngine(new()); var overlay = new LiveRiskOverlay();
        for (var second = 0; second <= 5; second++) {
            TrackedDetection[] detections = [Detection()];
            overlay.Update(second, detections, engine.Observe(second, detections), engine.ActiveTrackIds());
        }
        TrackedDetection[] faint = [Detection(.4, .3)];
        var decisions = engine.Observe(5.1, faint);
        var display = overlay.Update(5.1, faint, decisions, engine.ActiveTrackIds());
        Assert.Empty(decisions);
        var current = Assert.Single(display.Current);
        Assert.Equal("CRITICAL", current.Severity);
        Assert.Equal(faint[0].Box, current.Box);
        Assert.Equal(5.1, current.Timestamp);
        Assert.Equal(current, Assert.Single(display.Rendered));
    }

    [Fact]
    public void MissingPerson_DoesNotSeedNewViewersOrRenewHold_AndRecoveryRemovesColour()
    {
        var overlay = new LiveRiskOverlay();
        RiskDecision[] risk = [new("person", "episode", "CRITICAL", .9, 0, [.1, .1, .3, .5])];
        overlay.Update(0, [Detection()], risk, ["person"]);
        var missing = overlay.Update(.1, [], [], ["person"]);
        Assert.Single(missing.Rendered);
        Assert.Empty(missing.Current);
        Assert.Empty(overlay.Update(.31, [], [], ["person"]).Rendered);
        Assert.Empty(overlay.Update(.4, [Detection(.4)], [], []).Rendered);
    }

    [Fact]
    public void TrackingSessionReset_NeverTransfersOldRiskToReusedTrackId()
    {
        var overlay = new LiveRiskOverlay();
        overlay.Update(0, [Detection()], [new("person", "old-episode", "CRITICAL", .9, 0, [.1, .1, .3, .5])], ["person"]);
        overlay.Clear();
        Assert.Empty(overlay.Update(.1, [Detection(.4)], [], ["person"]).Rendered);
    }
}
