using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Disk outbox staging is independent of LIVE inference and video encoding.
public sealed class AlertSnapshotStagingWorker(AlertSnapshotQueue snapshots, ILogger<AlertSnapshotStagingWorker> logger) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        snapshots.CompleteStaging();
        try { if (ExecuteTask != null) await ExecuteTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken); }
        catch (TimeoutException) { logger.LogWarning("Snapshot staging shutdown timed out; no imageless alert was saved."); }
        finally { await base.StopAsync(cancellationToken); }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        DrainAsync(snapshots.CriticalStaging, stoppingToken), DrainAsync(snapshots.Staging, stoppingToken));

    private async Task DrainAsync(ChannelReader<SnapshotFrame> reader, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(stoppingToken))
            {
                // Retain the accepted frame until it is durable. A false return
                // means capacity/backpressure, never permission to discard it.
                while (!stoppingToken.IsCancellationRequested)
                {
                    try { if (await snapshots.StageAsync(frame.Command, frame.Jpeg, stoppingToken, frame.RenderedJpeg)) break; }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogWarning("Snapshot staging retry ({FailureType}); accepted frame retained in RAM.", exception.GetType().Name); }
                    await Task.Delay(200, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
