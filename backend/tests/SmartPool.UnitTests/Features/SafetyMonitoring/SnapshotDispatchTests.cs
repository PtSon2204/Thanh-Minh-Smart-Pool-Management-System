using System.Collections.Concurrent;
using CloudinaryDotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class SnapshotDispatchTests
{
    [Fact]
    public async Task SlowEpisode_DoesNotBlockOtherCameras_AndDuplicateEpisodeIsNotUploadedConcurrently()
    {
        var root = Path.Combine(Path.GetTempPath(), "smartpool-dispatch-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new MonitoringOptions { StoragePath = root });
        var files = new MonitoringFiles(options, Mock.Of<IHostEnvironment>());
        var queue = new AlertSnapshotQueue(files, options, NullLogger<AlertSnapshotQueue>.Instance);
        var slow = Guid.NewGuid(); var fast1 = Guid.NewGuid(); var fast2 = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recorded = new ConcurrentBag<Guid>(); var slowCalls = 0;
        var repository = new Mock<ISafetyMonitoringService>();
        repository.Setup(x => x.PrepareSnapshotAsync(It.IsAny<RecordRiskCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (RecordRiskCommand command, CancellationToken ct) => {
                if (command.CameraId == slow) {
                    Interlocked.Increment(ref slowCalls); entered.TrySetResult();
                    await release.Task.WaitAsync(ct);
                }
                return command.AlertId;
            });
        repository.Setup(x => x.RecordAsync(It.IsAny<RecordRiskCommand>(), It.IsAny<CancellationToken>()))
            .Returns((RecordRiskCommand command, CancellationToken ct) => {
                recorded.Add(command.CameraId);
                if (recorded.Contains(fast1) && recorded.Contains(fast2)) finished.TrySetResult();
                return Task.FromResult(new RiskWriteResult(new AlertDto(command.AlertId!.Value, command.CameraId,
                    "Camera", "Pool", "1", "episode", "CRITICAL", "PENDING", command.ImageUrl,
                    "COMPLETED", null, null, null, DateTime.UtcNow)));
            });
        using var services = new ServiceCollection().AddSingleton(repository.Object).BuildServiceProvider();
        using var worker = new SnapshotUploadWorker(services.GetRequiredService<IServiceScopeFactory>(), files, queue,
            new Cloudinary(new Account("unused", "unused", "unused")), options, NullLogger<SnapshotUploadWorker>.Instance);
        Task Add(Guid camera) {
            var id = Guid.NewGuid();
            return queue.SaveAsync(new(id, new(camera,
                new("1", "episode", "CRITICAL", .9, 1, [.1, .1, .3, .3]), .9, DateTime.UtcNow,
                "https://example.test/already-uploaded.jpg", id)), default);
        }
        try {
            await Add(slow); await worker.StartAsync(default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Add(slow); await Add(fast1); await Add(fast2);
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.DoesNotContain(slow, recorded);
            Assert.Equal(1, Volatile.Read(ref slowCalls));
        } finally {
            release.TrySetResult();
            await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
}
