using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class AlertSnapshotQueueTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "smartpool-snapshot-outbox-" + Guid.NewGuid().ToString("N"));
    private readonly MonitoringFiles files;
    private readonly IOptions<MonitoringOptions> options;
    public AlertSnapshotQueueTests()
    {
        options = Options.Create(new MonitoringOptions { StoragePath = root, MaxPendingAlertSnapshots = 16 });
        files = new MonitoringFiles(options, Mock.Of<IHostEnvironment>());
    }
    private AlertSnapshotQueue Queue() => new(files, options, NullLogger<AlertSnapshotQueue>.Instance);
    private static RecordRiskCommand Command(string episode = "episode") => new(Guid.NewGuid(),
        new("1", episode, "WARNING", .8, 1, [.1, .1, .4, .5]), .9, DateTime.UtcNow);

    [Fact]
    public async Task LiveStaging_NeverTouchesDiskOnProducer_BoundsMemory_AndDrainsOnShutdown()
    {
        var queue = Queue();
        var command = Command();
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xd9];
        Assert.False(queue.TryStage(command, [1, 2, 3, 4]));
        Assert.True(queue.TryStage(command, jpeg));
        for (var i = 0; i < 100; i++) Assert.True(queue.TryStage(command, jpeg));
        for (var i = 1; i < 16; i++) Assert.True(queue.TryStage(Command(episode: i.ToString()), jpeg));
        Assert.False(queue.TryStage(Command(), jpeg));
        Assert.False(Directory.Exists(root));
        using var worker = new AlertSnapshotStagingWorker(queue, NullLogger<AlertSnapshotStagingWorker>.Instance);
        await worker.StartAsync(default);
        await worker.StopAsync(default);
        Assert.Equal(16, queue.PendingFiles().Count());
        foreach (var path in queue.PendingFiles()) Assert.Null((await queue.ReadAsync(path, default)).Command.ImageUrl);
    }

    [Fact]
    public async Task OutboxStagesImmutableFrameAndSurvivesNewQueueInstance_BeforeDatabaseWrite()
    {
        var queue = Queue(); var command = Command(); var jpeg = new byte[] { 0xff, 0xd8, 0xff, 0xd9 };
        Assert.True(await queue.StageAsync(command, jpeg, default));
        Assert.True(await queue.StageAsync(command, [0xff, 0xd8, 1, 0xff, 0xd9], default));
        var path = Assert.Single(queue.PendingFiles());
        var recovered = Queue();
        var job = await recovered.ReadAsync(path, default);
        Assert.Null(job.Command.ImageUrl);
        Assert.Equal(command.CameraId, job.Command.CameraId);
        Assert.Equal(command.Decision, job.Command.Decision with { Box = command.Decision.Box });
        Assert.Equal(jpeg, await File.ReadAllBytesAsync(files.PathFor($"{job.Id:N}-source.jpg")));
        await recovered.SaveAsync(job with { Command = job.Command with { ImageUrl = "https://example.test/uploaded.jpg" } }, default);
        Assert.Equal("https://example.test/uploaded.jpg", (await recovered.ReadAsync(path, default)).Command.ImageUrl);
        recovered.Complete(job.Id);
        Assert.Empty(recovered.PendingFiles());
    }

    [Fact]
    public async Task LiveStaging_ReusesRenderedPixels_AndRecoveryPrioritisesLiveCritical()
    {
        var queue = Queue();
        byte[] source = [0xff, 0xd8, 1, 0xff, 0xd9], rendered = [0xff, 0xd8, 2, 0xff, 0xd9];
        var warning = Command();
        var critical = Command() with { Decision = Command().Decision with { Severity = "CRITICAL" } };
        await queue.StageAsync(Command("stale-episode"), source, default);
        Assert.True(queue.TryStage(warning, source, rendered));
        Assert.True(queue.TryStage(critical, source, rendered));
        using var worker = new AlertSnapshotStagingWorker(queue, NullLogger<AlertSnapshotStagingWorker>.Instance);
        await worker.StartAsync(default); await worker.StopAsync(default);
        var recovered = Queue();
        var first = await recovered.ReadAsync(recovered.PendingFiles().First(), default);
        Assert.Equal("CRITICAL", first.Command.Decision.Severity);
        Assert.Equal(rendered, await File.ReadAllBytesAsync(files.PathFor($"{first.Id:N}.jpg")));
        Assert.Equal(source, await File.ReadAllBytesAsync(files.PathFor($"{first.Id:N}-source.jpg")));
        Assert.Null(first.Command.ImageUrl);
        await Assert.ThrowsAsync<ArgumentException>(() => recovered.StageAsync(Command(), source, default, [1, 2, 3, 4]));
    }

    [Fact]
    public async Task OutboxIsBoundedAndInvalidJpegIsNotAccepted()
    {
        var queue = Queue();
        await Assert.ThrowsAsync<ArgumentException>(() => queue.StageAsync(Command(), [1, 2, 3, 4], default));
        for (var i = 0; i < 16; i++) Assert.True(await queue.StageAsync(Command(episode: i.ToString()), [0xff, 0xd8, 0xff, 0xd9], default));
        Assert.False(await queue.StageAsync(Command(), [0xff, 0xd8, 0xff, 0xd9], default));
    }
    public void Dispose()
    {
        if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
