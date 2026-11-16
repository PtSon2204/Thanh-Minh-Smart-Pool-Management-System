using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraAnalysisBroadcastDiagnosticsTests
{
    private static CameraDiagnostics CreateDiagnostics() =>
        new(Options.Create(new CameraOptions { Diagnostics = new CameraDiagnosticsOptions { Enabled = true } }));

    [Fact]
    public async Task WorkerRecordsBroadcastOutcomeWithPayloadIdentityAndTiming()
    {
        var id = Guid.NewGuid();
        var cameras = new Mock<ICameraService>();
        cameras.Setup(x => x.GetStatus(id)).Returns(new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        var file = typeof(CameraAnalysisState).Assembly.Location;
        var state = new CameraAnalysisState(cameras.Object, Options.Create(new MonitoringOptions {
            Enabled = true, PythonPath = file, WorkerPath = file, ModelPath = file }));
        var diagnostics = CreateDiagnostics();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendToGroupAsync(It.IsAny<string>(), "camera_analysis", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection()
            .AddSingleton(notifications.Object)
            .AddSingleton(diagnostics)
            .BuildServiceProvider();
        using var worker = new CameraAnalysisBroadcastWorker(state, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CameraAnalysisBroadcastWorker>.Instance, diagnostics);
        state.Set(id, "frame-1", DateTimeOffset.UtcNow, [], [1]);
        await worker.StartAsync(default);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                var events = diagnostics.Get(id);
                if (events.Any(x => x.Kind == "broadcast_success" && x.FrameId == "frame-1")) break;
                await Task.Delay(20);
            }
            var success = diagnostics.Get(id).First(x => x.Kind == "broadcast_success");
            Assert.Equal("frame-1", success.FrameId);
            Assert.NotNull(success.ElapsedMilliseconds);
            Assert.DoesNotContain(diagnostics.Get(id), x => x.Kind == "broadcast_failure");
        }
        finally { await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public void BroadcastFailuresAreCountedPerCamera()
    {
        var id = Guid.NewGuid();
        var diagnostics = CreateDiagnostics();
        for (var i = 0; i < 3; i++)
            diagnostics.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "broadcast_failure", id,
                FrameId: $"f{i}", FailureReason: "timeout"));
        Assert.Equal(3, diagnostics.BroadcastCount(id).Failures);
        diagnostics.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "broadcast_success", id, FrameId: "ok"));
        Assert.Equal(1, diagnostics.BroadcastCount(id).Successes);
        Assert.Equal(3, diagnostics.BroadcastCount(id).Failures);
    }

    [Fact]
    public async Task ThreeCamerasStayBoundedAndIndependent_WithoutWorseningHealthyBroadcasts()
    {
        // Chạy đồng thời 3 camera: mỗi camera có ring buffer + counter riêng, không
        // ảnh hưởng nhau; camera khoẻ vẫn phát trong <1s dù 2 camera khác tắc.
        var cameras = new Mock<ICameraService>();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        cameras.Setup(x => x.GetStatus(It.IsAny<Guid>()))
            .Returns((Guid g) => new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        var file = typeof(CameraAnalysisState).Assembly.Location;
        var state = new CameraAnalysisState(cameras.Object, Options.Create(new MonitoringOptions {
            Enabled = true, PythonPath = file, WorkerPath = file, ModelPath = file }));
        var diagnostics = CreateDiagnostics();
        var healthy = ids[0];
        var slow = ids[1];
        var slow2 = ids[2];
        var healthyDelivered = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendToGroupAsync(It.IsAny<string>(), "camera_analysis", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(async (string group, string name, object payload, CancellationToken ct) => {
                if (group == $"camera-analysis_{healthy:N}") healthyDelivered.TrySetResult(clock.Elapsed.TotalMilliseconds);
                else await Task.Delay(5000, ct); // 2 camera khác tắc (client chậm).
            });
        using var services = new ServiceCollection()
            .AddSingleton(notifications.Object)
            .AddSingleton(diagnostics)
            .BuildServiceProvider();
        using var worker = new CameraAnalysisBroadcastWorker(state, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CameraAnalysisBroadcastWorker>.Instance, diagnostics);
        for (var i = 0; i < 3; i++) state.Set(ids[i], "frame-0", DateTimeOffset.UtcNow, [], [1]);
        await worker.StartAsync(default);
        try
        {
            var latency = await healthyDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(latency < 1000, $"Healthy camera blocked by {latency:F0} ms; three-camera load must not stall it.");
            // Each camera keeps its own bounded events; counters are per camera.
            Assert.True(diagnostics.Get(healthy).Count <= 512);
            Assert.True(diagnostics.Get(slow).Count <= 512);
            Assert.Equal(0, diagnostics.BroadcastCount(healthy).Failures);
        }
        finally { await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(3)); }
    }
}
