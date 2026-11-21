using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring.Audit;

public sealed class RiskBoundaryAuditTests
{
    private static TrackedDetection Detection(double confidence = .9, double[]? box = null, string label = "drowning", string id = "1") =>
        new(id, label, confidence, box ?? [.1, .1, .8, .8]);

    [Theory]
    [InlineData("R01", .599999, false)]
    [InlineData("R02", .6, true)]
    [InlineData("R03", .600001, true)]
    [InlineData("R04", 1, true)]
    [InlineData("R05", 1.000001, false)]
    [InlineData("R06", double.NaN, false)]
    [InlineData("R07", double.PositiveInfinity, false)]
    [InlineData("R08", -.1, false)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_ConfidenceBoundary_CountsOnlyAcceptedEvidence(string utcId, double confidence, bool accepted)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var engine = new RiskDecisionEngine(new() { MinObservations = 2 });
        Assert.Empty(engine.Observe(0, [Detection(confidence)]));
        Assert.Equal(accepted ? 1 : 0, engine.Observe(1, [Detection(confidence)]).Count);
    }

    [Theory]
    [InlineData("R09", double.NaN)]
    [InlineData("R10", double.PositiveInfinity)]
    [InlineData("R11", double.NegativeInfinity)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_NonFiniteTimestamp_ThrowsBeforeCreatingTracks(string utcId, double timestamp)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var engine = new RiskDecisionEngine(new());
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Observe(timestamp, [Detection()]));
        Assert.Empty(engine.ActiveTrackIds());
    }

    [Theory]
    [InlineData("R12", "length")]
    [InlineData("R13", "negative")]
    [InlineData("R14", "aboveOne")]
    [InlineData("R15", "zeroWidth")]
    [InlineData("R16", "reversedHeight")]
    [InlineData("R17", "nan")]
    [InlineData("R18", "class")]
    [InlineData("R19", "track")]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_InvalidDetection_DoesNotAccumulateEvidence(string utcId, string invalid)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var box = invalid switch {
            "length" => new double[] { 0, 0, 1 }, "negative" => [-.1, 0, 1, 1],
            "aboveOne" => [0, 0, 1.1, 1], "zeroWidth" => [.1, 0, .1, 1],
            "reversedHeight" => [0, .8, 1, .2], "nan" => [0, 0, double.NaN, 1], _ => [0, 0, 1, 1] };
        var detection = Detection(box: box, label: invalid == "class" ? "person" : "drowning", id: invalid == "track" ? " " : "1");
        var engine = new RiskDecisionEngine(new() { MinObservations = 2 });
        Assert.Empty(engine.Observe(0, [detection]));
        Assert.Empty(engine.Observe(1, [detection]));
        Assert.Empty(engine.ActiveTrackIds());
    }

    [Theory]
    [InlineData("R20", 2.999999, "WARNING")]
    [InlineData("R21", 3, "CRITICAL")]
    [InlineData("R22", 3.000001, "CRITICAL")]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_CriticalHoldBoundary_EscalatesAtInclusiveLimit(string utcId, double elapsed, string severity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var engine = new RiskDecisionEngine(new() { MinObservations = 2, MaxObservationGapSeconds = 10 });
        engine.Observe(0, [Detection()]);
        var warning = Assert.Single(engine.Observe(1, [Detection()]));
        var actual = Assert.Single(engine.Observe(1 + elapsed, [Detection()]));
        Assert.Equal(severity, actual.Severity);
        Assert.Equal(warning.Episode, actual.Episode);
    }

    [Theory]
    [InlineData("R23", 120, true)]
    [InlineData("R24", 120.000001, false)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_TrackRetentionBoundary_RemovesOnlyAfterLimit(string utcId, double age, bool retained)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var engine = new RiskDecisionEngine(new() { MinObservations = 2 });
        engine.Observe(0, [Detection()]); engine.Observe(1, [Detection()]);
        Assert.Empty(engine.Observe(1 + age, []));
        Assert.Equal(retained, engine.ActiveTrackIds().Contains("1"));
    }

    [Fact]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_DuplicateTrack_UsesHighestConfidenceOncePerTimestamp()
    {
        var engine = new RiskDecisionEngine(new() { MinObservations = 2 });
        Assert.Empty(engine.Observe(0, [Detection(.7, label: "normal"), Detection(.9)]));
        Assert.Empty(engine.Observe(0, [Detection()]));
        Assert.Equal(1, Assert.Single(engine.Observe(1, [Detection()])).Ratio);
    }

    [Theory]
    [InlineData("R25", .999999, true)]
    [InlineData("R26", 1, true)]
    [InlineData("R27", 1.000001, false)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_WindowBoundary_RetainsEvidenceAtInclusiveLimit(string utcId, double timestamp, bool emitted)
    {
        _ = utcId;
        var engine = new RiskDecisionEngine(new() { WindowSeconds = 1, MinObservations = 2 });
        engine.Observe(0, [Detection()]);
        Assert.Equal(emitted ? 1 : 0, engine.Observe(timestamp, [Detection()]).Count);
    }

    [Theory]
    [InlineData("R28", 2.999999, true)]
    [InlineData("R29", 3, true)]
    [InlineData("R30", 3.000001, false)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_ObservationGapBoundary_ResetsWindowOnlyAfterLimit(string utcId, double timestamp, bool emitted)
    {
        _ = utcId;
        var engine = new RiskDecisionEngine(new() { WindowSeconds = 10, MinObservations = 2 });
        engine.Observe(0, [Detection()]);
        Assert.Equal(emitted ? 1 : 0, engine.Observe(timestamp, [Detection()]).Count);
    }

    [Theory]
    [InlineData("R31", .499999, true)]
    [InlineData("R32", .5, false)]
    [InlineData("R33", .500001, false)]
    [Trait("Function", "RiskDecisionEngine.Observe")]
    public void Observe_RecoveryHoldBoundary_RequiresSustainedNormalEvidence(string utcId, double elapsed, bool active)
    {
        _ = utcId;
        var engine = new RiskDecisionEngine(new() { WindowSeconds = 1, MinObservations = 2, RecoveryHoldSeconds = .5 });
        engine.Observe(0, [Detection()]); engine.Observe(.1, [Detection()]);
        engine.Observe(1.2, [Detection(label: "normal")]); engine.Observe(1.3, [Detection(label: "normal")]);
        Assert.Equal(active ? 1 : 0, engine.Observe(1.3 + elapsed, [Detection(label: "normal")]).Count);
        Assert.Equal(active, engine.ActiveTrackIds().Contains("1"));
    }

    [Theory]
    [InlineData("O01", "confidence", 0, true)]
    [InlineData("O02", "confidence", 1, true)]
    [InlineData("O03", "confidence", -.000001, false)]
    [InlineData("O04", "confidence", 1.000001, false)]
    [InlineData("O05", "confidence", double.NaN, false)]
    [InlineData("O06", "window", 0, false)]
    [InlineData("O07", "window", .000001, true)]
    [InlineData("O08", "hold", 0, false)]
    [InlineData("O09", "recoveryHold", 0, false)]
    [InlineData("O10", "gap", 0, false)]
    [InlineData("O11", "cooldown", 0, true)]
    [InlineData("O12", "cooldown", -.000001, false)]
    [InlineData("O13", "observations", 1, false)]
    [InlineData("O14", "observations", 2, true)]
    [InlineData("O15", "warning", .7, false)]
    [InlineData("O16", "recovery", .4, false)]
    [InlineData("O17", "critical", 1, true)]
    [InlineData("O18", "critical", 1.000001, false)]
    [Trait("Function", "RiskOptions.IsValid")]
    public void IsValid_ThresholdLimits_ReturnsSourceDefinedValidity(string utcId, string field, double value, bool expected)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var options = new RiskOptions();
        switch (field) {
            case "confidence": options.ConfidenceThreshold = value; break;
            case "window": options.WindowSeconds = value; break;
            case "hold": options.CriticalHoldSeconds = value; break;
            case "recoveryHold": options.RecoveryHoldSeconds = value; break;
            case "gap": options.MaxObservationGapSeconds = value; break;
            case "cooldown": options.CooldownSeconds = value; break;
            case "observations": options.MinObservations = (int)value; break;
            case "warning": options.WarningRatio = value; break;
            case "recovery": options.RecoveryRatio = value; break;
            case "critical": options.CriticalRatio = value; break;
        }
        Assert.Equal(expected, options.IsValid());
    }
}
