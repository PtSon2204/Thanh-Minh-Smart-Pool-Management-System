using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;
using Xunit;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

// Regression for accepted frames and the bounded LIVE critical reserve.
public sealed class OutboxRegressionTests
{
    [Fact]
    public async Task F02_CriticalAcceptedIntoRam_ShouldNotDisappearWhenDiskOutboxIsFull()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "smartpool-audit-" + Guid.NewGuid().ToString("N")));
        var options = Options.Create(new MonitoringOptions { StoragePath = root, MaxPendingAlertSnapshots = 16 });
        var files = new MonitoringFiles(options, Mock.Of<IHostEnvironment>());
        var queue = new AlertSnapshotQueue(files, options, NullLogger<AlertSnapshotQueue>.Instance);
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xd9];
        var camera = Guid.NewGuid();
        RecordRiskCommand Event(string episode, string severity) => new(camera,
            new("1", episode, severity, .9, 1, [.1, .1, .3, .3]), .9, DateTime.UtcNow);
        try
        {
            for (var i = 0; i < 16; i++)
                Assert.True(await queue.StageAsync(Event("old-" + i, "WARNING"), jpeg, default));
            Assert.True(queue.TryStage(Event("critical-new", "CRITICAL"), jpeg));
            using var worker = new AlertSnapshotStagingWorker(queue, NullLogger<AlertSnapshotStagingWorker>.Instance);
            await worker.StartAsync(default);
            await worker.StopAsync(default);
            var persisted = new List<PendingAlertSnapshot>();
            foreach (var path in queue.PendingFiles()) persisted.Add(await queue.ReadAsync(path, default));
            Assert.Contains(persisted, job => job.Command.Decision.Episode == "critical-new");
        }
        finally
        {
            var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
