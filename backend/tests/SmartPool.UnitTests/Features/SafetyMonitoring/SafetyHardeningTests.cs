using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class SafetyHardeningTests
{
    [Fact]
    public void ConnectedCamera_TakesPriorityOverOfflineWorkers_AndExpiredDisconnectsReleaseSlots()
    {
        var now = DateTimeOffset.UtcNow;
        var offline = Guid.NewGuid(); var connected = Guid.NewGuid(); var browser = Guid.NewGuid();
        var running = new HashSet<Guid> { offline };
        (Guid, CameraConnectionStatus)[] candidates = [
            (offline, new("Disconnected", now.AddSeconds(-2))),
            (connected, new("Connected", now)), (browser, new("Disconnected", null))];
        Assert.Equal(new[] { connected }, LiveCameraSelection.Select(candidates, running, 1, now, TimeSpan.FromSeconds(15)));
        Assert.Equal(new[] { offline }, LiveCameraSelection.Select(candidates.Take(1), running, 1, now, TimeSpan.FromSeconds(15)));
        Assert.Empty(LiveCameraSelection.Select(candidates.Take(1), running, 1, now.AddSeconds(16), TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void ConnectedWorkers_AreStableWhenOtherConnectedCamerasCompeteForCapacity()
    {
        var now = DateTimeOffset.UtcNow; var current = Guid.NewGuid(); var other = Guid.NewGuid();
        (Guid, CameraConnectionStatus)[] candidates = [(other, new("Connected", now)), (current, new("Connected", now))];
        Assert.Equal(new[] { current }, LiveCameraSelection.Select(candidates, new HashSet<Guid> { current }, 1, now, TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public async Task ThresholdSnapshots_NeverMixTwoConcurrentVersions()
    {
        var first = new RiskOptions { ConfidenceThreshold = .6, MinObservations = 2, WindowSeconds = 3 };
        var second = new RiskOptions { ConfidenceThreshold = .9, MinObservations = 5, WindowSeconds = 8 };
        var shared = first.Clone();
        using var start = new Barrier(2);
        await Task.WhenAll(Task.Run(() => {
            start.SignalAndWait();
            for (var i = 0; i < 20000; i++) shared.CopyFrom(i % 2 == 0 ? first : second);
        }), Task.Run(() => {
            start.SignalAndWait();
            for (var i = 0; i < 20000; i++) {
                var snapshot = shared.Clone();
                Assert.True(snapshot.ConfidenceThreshold == .6 && snapshot.MinObservations == 2 && snapshot.WindowSeconds == 3 ||
                    snapshot.ConfidenceThreshold == .9 && snapshot.MinObservations == 5 && snapshot.WindowSeconds == 8);
            }
        }));
    }

    [Fact]
    public async Task FullDiskOutbox_RetainsAcceptedWarningUntilCapacityReturns_AndReservesCriticalRam()
    {
        var root = Path.Combine(Path.GetTempPath(), "smartpool-hardening-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new MonitoringOptions { StoragePath = root, MaxPendingAlertSnapshots = 16 });
        var files = new MonitoringFiles(options, Mock.Of<IHostEnvironment>());
        var queue = new AlertSnapshotQueue(files, options, NullLogger<AlertSnapshotQueue>.Instance);
        RecordRiskCommand Command(string episode, string severity = "WARNING") => new(Guid.NewGuid(),
            new("1", episode, severity, .9, 1, [.1, .1, .3, .3]), .9, DateTime.UtcNow);
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xd9];
        using var worker = new AlertSnapshotStagingWorker(queue, NullLogger<AlertSnapshotStagingWorker>.Instance);
        try
        {
            for (var i = 0; i < 16; i++) await queue.StageAsync(Command("disk-" + i), jpeg, default);
            var pending = Command("accepted-warning");
            Assert.True(queue.TryStage(pending, jpeg));
            await worker.StartAsync(default);
            await Task.Delay(250); // A failed first disk attempt must not discard the accepted frame.
            foreach (var path in queue.PendingFiles().ToArray()) queue.Complete((await queue.ReadAsync(path, default)).Id);
            await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(4));
            var saved = await queue.ReadAsync(Assert.Single(queue.PendingFiles()), default);
            Assert.Equal("accepted-warning", saved.Command.Decision.Episode);

            var ram = new AlertSnapshotQueue(files, options, NullLogger<AlertSnapshotQueue>.Instance);
            for (var i = 0; i < 16; i++) Assert.True(ram.TryStage(Command("ram-" + i), jpeg));
            Assert.False(ram.TryStage(Command("overflow"), jpeg));
            Assert.Equal(1, ram.RejectedSnapshots);
            Assert.True(ram.TryStage(Command("reserved-critical", "CRITICAL"), jpeg));
        }
        finally
        {
            await worker.StopAsync(default);
            var resolved = Path.GetFullPath(root);
            var allowed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}
