using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraAnalysisStateTests
{
    [Fact]
    public void FrameRead_AfterNextInference_StillReturnsTheRequestedPixels_AndExpiresOldFrames()
    {
        var id = Guid.NewGuid();
        var cameras = new Mock<ICameraService>();
        cameras.Setup(x => x.GetStatus(It.IsAny<Guid>())).Returns(new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        var existing = typeof(CameraAnalysisState).Assembly.Location;
        var state = new CameraAnalysisState(cameras.Object, Options.Create(new MonitoringOptions {
            Enabled = true, PythonPath = existing, WorkerPath = existing, ModelPath = existing }));
        state.Set(id, "old", DateTimeOffset.UtcNow.AddSeconds(-7), [], [9]);
        state.Set(id, "first", DateTimeOffset.UtcNow, [], [1]);
        state.Set(id, "second", DateTimeOffset.UtcNow, [], [2], inferenceMilliseconds: 10,
            activeRiskTrackIds: ["track"], renderMilliseconds: 2, analysisMilliseconds: 14,
            displayRisks: [new("track", "episode", "CRITICAL", .9, 0, [.1, .1, .3, .5])]);
        Assert.Empty(state.Get(id).Risks);
        Assert.Equal("CRITICAL", Assert.Single(state.Get(id).DisplayRisks!).Severity);
        Assert.Equal(new[] { "track" }, state.Get(id).ActiveRiskTrackIds);
        Assert.Equal(10, state.Get(id).InferenceMilliseconds);
        Assert.Equal(2, state.Get(id).RenderMilliseconds);
        Assert.Equal(14, state.Get(id).AnalysisMilliseconds);
        Assert.Equal(new byte[] { 1 }, state.Frame(id, "first"));
        Assert.Equal(new byte[] { 2 }, state.Frame(id, "second"));
        Assert.Null(state.Frame(id, "old"));
        Assert.Null(state.Frame(Guid.NewGuid(), "first"));
        for (var i = 0; i < 9; i++) state.Set(id, $"new-{i}", DateTimeOffset.UtcNow.AddMilliseconds(i), [], [3]);
        Assert.Null(state.Frame(id, "first"));
        state.Remove(id);
        Assert.Null(state.Frame(id, "new-8"));
    }
}
