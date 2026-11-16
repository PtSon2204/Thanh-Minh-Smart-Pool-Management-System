using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;
using Xunit;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class BroadcastRegressionTests
{
    [Fact]
    public async Task F05_HealthyCameraMetadata_ShouldNotWaitForSevenOtherSendTimeouts()
    {
        var cameras = new Mock<ICameraService>();
        cameras.Setup(x => x.GetStatus(It.IsAny<Guid>())).Returns(new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        var file = typeof(CameraAnalysisState).Assembly.Location;
        var state = new CameraAnalysisState(cameras.Object, Options.Create(new MonitoringOptions {
            Enabled = true, PythonPath = file, WorkerPath = file, ModelPath = file }));
        for (var i = 0; i < 8; i++) state.Set(Guid.NewGuid(), "frame-" + i, DateTimeOffset.UtcNow, [], [1]);
        // The production worker enumerates this same stable set. Put the healthy
        // destination last to reproduce the worst case without relying on GUID order.
        var healthy = state.Active()[^1].CameraId!.Value;
        var delivered = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = Stopwatch.StartNew();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendToGroupAsync(It.IsAny<string>(), "camera_analysis", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(async (string group, string name, object payload, CancellationToken ct) => {
                if (group == $"camera-analysis_{healthy:N}") delivered.TrySetResult(clock.Elapsed.TotalMilliseconds);
                else await Task.Delay(5000, ct);
            });
        using var services = new ServiceCollection().AddSingleton(notifications.Object).BuildServiceProvider();
        using var worker = new CameraAnalysisBroadcastWorker(state, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CameraAnalysisBroadcastWorker>.Instance);
        try
        {
            await worker.StartAsync(default);
            var latency = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(latency < 1000, $"Healthy camera waited {latency:F0} ms; frontend overlay expires at 1000 ms.");
        }
        finally { await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(3)); }
    }
}
