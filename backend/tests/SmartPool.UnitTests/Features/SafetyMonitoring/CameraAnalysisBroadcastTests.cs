using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraAnalysisBroadcastTests
{
    [Fact]
    public async Task CoalescesMetadata_WithoutBlockingFramesOnSlowClients()
    {
        var id = Guid.NewGuid();
        var cameras = new Mock<ICameraService>();
        cameras.Setup(x => x.GetStatus(id)).Returns(new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        var file = typeof(CameraAnalysisState).Assembly.Location;
        var state = new CameraAnalysisState(cameras.Object, Options.Create(new MonitoringOptions {
            Enabled = true, PythonPath = file, WorkerPath = file, ModelPath = file }));
        var sent = new ConcurrentQueue<string>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendToGroupAsync(It.IsAny<string>(), "camera_analysis", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(async (string group, string name, object payload, CancellationToken ct) => {
                sent.Enqueue(((CameraAnalysisDto)payload).FrameId!);
                entered.TrySetResult();
                await Task.Delay(5000, ct);
            });
        using var services = new ServiceCollection().AddSingleton(notifications.Object).BuildServiceProvider();
        using var worker = new CameraAnalysisBroadcastWorker(state, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CameraAnalysisBroadcastWorker>.Instance);
        var frameCount = 0;
        state.FrameReady += (_, _, _) => Interlocked.Increment(ref frameCount);
        state.Set(id, "first", DateTimeOffset.UtcNow, [], [1]);
        await worker.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 100; i++) state.Set(id, $"frame-{i}", DateTimeOffset.UtcNow, [], [1]);
        Assert.Equal(101, frameCount);
        Assert.Equal("frame-99", state.Get(id).FrameId);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!sent.Contains("frame-99") && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.Contains("frame-99", sent);
        Assert.True(sent.Count < 10, "Metadata should coalesce instead of replaying every frame to a slow client.");
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(2));
    }
}
