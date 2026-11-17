using Microsoft.Extensions.Options;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraDiagnosticsTests
{
    private static CameraDiagnostics CreateDiagnostics(bool enabled, int maxEvents = 4, int sampleMs = 50) =>
        new(Options.Create(new CameraOptions { Diagnostics = new CameraDiagnosticsOptions
        {
            Enabled = enabled,
            MaxEventsPerCamera = maxEvents,
            FrameSampleMilliseconds = sampleMs
        } }));

    [Fact]
    public void DiagnosticsDisabled_ByDefault_RecordsNothingAndDoesNotSample()
    {
        // A plain CameraOptions has Diagnostics defaulting to Enabled=false.
        var diagnostics = CreateDiagnostics(enabled: false);
        Assert.False(diagnostics.Enabled);
        Assert.False(diagnostics.ShouldSampleFrame(Guid.NewGuid(), DateTimeOffset.UtcNow));

        var cameraId = Guid.NewGuid();
        diagnostics.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "camera_analysis_frame", cameraId));
        Assert.Empty(diagnostics.Get(cameraId));
    }

    [Fact]
    public void DiagnosticsEnabled_RecordsOnlyWhileOn_AndBoundsThePerCameraQueue()
    {
        var cameraId = Guid.NewGuid();
        var diagnostics = CreateDiagnostics(enabled: true, maxEvents: 32);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 40; i++)
            diagnostics.Record(new CameraDiagnosticEvent(now.AddMilliseconds(i * 10), "camera_analysis_frame", cameraId,
                FrameId: $"f{i}", RtpTimestamp: 10000u + (uint)i * 90,
                InferenceMilliseconds: 40, DetectionCount: 2,
                Detections: [new("t1", "drowning", .9, new double[] { .1, .2, .3, .4 }),
                    new("t2", "normal", .4, new double[] { .4, .5, .6, .7 })],
                ActiveRiskTrackIds: ["t1"],
                RenderedRiskBoxes: [new DiagnosticBox("t1", "ep", "CRITICAL", new double[] { .1, .2, .3, .4 })],
                CurrentRiskBoxes: [new DiagnosticBox("t1", "ep", "CRITICAL", new double[] { .1, .2, .3, .4 })],
                RiskDecisionCount: 1, DisplayRiskCount: 1,
                CameraSession: "cam-session", WorkerSession: "worker-session"));

        var events = diagnostics.Get(cameraId, limit: 100);
        Assert.Equal(32, events.Count);
        Assert.Equal("f8", events[0].FrameId); // oldest 8 entries dropped, newest 32 kept
        Assert.Equal("f39", events[^1].FrameId);
        var first = events[0];
        Assert.Equal("camera_analysis_frame", first.Kind);
        Assert.Equal(cameraId, first.CameraId);
        Assert.Equal(2, first.DetectionCount);
        Assert.Equal("t1", first.Detections![0].TrackId);
        Assert.Equal(.4, first.Detections[1].Confidence);
        Assert.Equal(1, first.RiskDecisionCount);
        Assert.Equal(1, first.DisplayRiskCount);
        Assert.Equal(new[] { "t1" }, first.ActiveRiskTrackIds);
        Assert.Equal("drowning", first.Detections![0].Class);
        Assert.Equal(new[] { .1, .2, .3, .4 }, first.Detections[0].Box);
        Assert.Equal("cam-session", first.CameraSession);
        Assert.Equal("worker-session", first.WorkerSession);

        // No JPEG bytes, URLs or credentials: event shape is bounded and enumerable.
        Assert.DoesNotContain("rtsp", string.Join("|", events));
    }

    [Fact]
    public void FrameSamplingIsRateLimited()
    {
        var cameraId = Guid.NewGuid();
        var diagnostics = CreateDiagnostics(enabled: true, sampleMs: 50);
        var now = DateTimeOffset.UtcNow;
        Assert.True(diagnostics.ShouldSampleFrame(cameraId, now));
        Assert.False(diagnostics.ShouldSampleFrame(cameraId, now.AddMilliseconds(10)), "Second sample within the interval is suppressed");
        Assert.True(diagnostics.ShouldSampleFrame(cameraId, now.AddMilliseconds(60)), "A later sample is allowed");

        // Independent camera: no cross-camera influence.
        var other = Guid.NewGuid();
        Assert.True(diagnostics.ShouldSampleFrame(other, now));
    }

    [Fact]
    public void RestartEventsAreRecordedLikeAnyOtherDiagnosticEvent()
    {
        var cameraId = Guid.NewGuid();
        var diagnostics = CreateDiagnostics(enabled: true, maxEvents: 4);
        diagnostics.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "decoder_restart", cameraId,
            CameraSession: "cam-session", RestartReason: "DecodeError"));
        var events = diagnostics.Get(cameraId);
        Assert.Equal("decoder_restart", events[0].Kind);
        Assert.Equal("DecodeError", events[0].RestartReason);

        diagnostics.Clear(cameraId);
        Assert.Empty(diagnostics.Get(cameraId));
    }
}
