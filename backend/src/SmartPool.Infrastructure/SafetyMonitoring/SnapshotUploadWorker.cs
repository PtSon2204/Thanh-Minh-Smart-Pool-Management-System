using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed partial class SnapshotUploadWorker(IServiceScopeFactory scopes, MonitoringFiles files, AlertSnapshotQueue outbox, Cloudinary cloudinary, IOptions<MonitoringOptions> options,
    ILogger<SnapshotUploadWorker> logger) : BackgroundService
{
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(30);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> activeEpisodes = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One slow upload must not serialize alerts from every camera. Each item
        // still has its own filesystem lease, DB scope, timeout and durable retry.
        await Task.WhenAll(RunOutboxAsync(stoppingToken), RunOutboxAsync(stoppingToken),
            RunOutboxAsync(stoppingToken), RunLegacyAsync(stoppingToken));
    }

    private async Task RunOutboxAsync(CancellationToken ct)
    {
        try {
            while (!ct.IsCancellationRequested) {
                try { if (await ProcessOneOutboxAsync(ct)) continue; }
                catch (Exception) when (!ct.IsCancellationRequested) { logger.LogWarning("Snapshot outbox unavailable; retrying."); }
                await outbox.WaitForWorkAsync(ct);
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task<bool> ProcessOneOutboxAsync(CancellationToken ct)
    {
        foreach (var path in outbox.PendingFiles())
        {
            FileStream? lease = null;
            PendingAlertSnapshot? job = null;
            var claimed = false;
            string? episodeKey = null;
            try
            {
                lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                job = await outbox.ReadAsync(path, ct);
                if (job.NextAttemptAt > DateTimeOffset.UtcNow) continue;
                var key = $"LIVE:{job.Command.CameraId}:{job.Command.Decision.TrackId}:{job.Command.Decision.Episode}";
                if (!activeEpisodes.TryAdd(key, 0)) continue;
                episodeKey = key;
                claimed = true;
                var started = Stopwatch.GetTimestamp();
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISafetyMonitoringService>();
                var target = await repository.PrepareSnapshotAsync(job.Command, ct);
                if (target == null) { outbox.Complete(job.Id); return true; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(UploadTimeout);
                if (string.IsNullOrWhiteSpace(job.Command.ImageUrl))
                {
                    // Cố định public_id trước upload để retry không nhân bản asset.
                    job = job with { Command = job.Command with { AlertId = target } };
                    await outbox.SaveAsync(job, ct);
                    // Job ID keeps this observation's asset separate from a concurrently handled warning.
                    var imageUrl = await UploadSnapshotAsync(job.Id, timeout.Token);
                    job = job with { Command = job.Command with { ImageUrl = imageUrl } };
                    await outbox.SaveAsync(job, ct);
                }
                // Application commits the image-backed alert and decides which events to publish.
                var recorded = await repository.RecordAsync(job.Command, timeout.Token);
                logger.LogInformation("Alert {AlertId} saved with image: source {Source}, processing {ProcessingMs:F0} ms, observation age {ObservationAgeMs:F0} ms.",
                    recorded.Alert.Id, job.Command.CameraId, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    (DateTime.UtcNow - DateTime.SpecifyKind(job.Command.ObservedAt, DateTimeKind.Utc)).TotalMilliseconds);
                outbox.Complete(job.Id);
            }
            catch (IOException) when (lease == null) { /* Một worker khác đang giữ lease. */ }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                if (job != null)
                {
                    job = job with { Attempts = Math.Min(job.Attempts + 1, 30),
                        NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(job.Attempts + 1, 6)))) };
                    await outbox.SaveAsync(job, ct);
                    logger.LogWarning("Snapshot {SnapshotId} deferred ({FailureType}); alert has not been saved without an image.", job.Id, exception.GetType().Name);
                }
                else logger.LogWarning("Invalid snapshot outbox item; no alert was saved.");
            }
            finally
            {
                if (episodeKey != null) activeEpisodes.TryRemove(episodeKey, out _);
                if (lease != null)
                {
                    await lease.DisposeAsync();
                    try { File.Delete(path + ".lock"); } catch (IOException) { /* Worker khác vừa nhận lease. */ }
                }
            }
            if (claimed) return true;
        }
        return false;
    }

    private async Task<string> UploadSnapshotAsync(Guid assetId, CancellationToken ct)
    {
        var filename = $"{assetId:N}.jpg";
        var path = files.PathFor(filename);
        if (!File.Exists(path)) await RenderAsync(assetId, ct);
        await using var image = File.OpenRead(path);
        var uploaded = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(filename, image),
            Folder = "smart-pool/incidents",
            PublicId = assetId.ToString("N"),
            Overwrite = true
        }, ct);
        if (uploaded.Error != null || uploaded.SecureUrl == null) throw new IOException("Snapshot upload failed.");
        return uploaded.SecureUrl.AbsoluteUri;
    }

    private async Task RenderAsync(Guid id, CancellationToken ct)
    {
        var start = new ProcessStartInfo(options.Value.PythonPath) { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(Path.GetDirectoryName(options.Value.WorkerPath)!, "snapshot.py"),
            files.PathFor($"{id:N}-source.jpg"), files.PathFor($"{id:N}-boxes.json"), files.PathFor($"{id:N}.jpg"), options.Value.FontPath }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Snapshot renderer unavailable.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception) { } });
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); await output; await error;
        if (process.ExitCode != 0 || !File.Exists(files.PathFor($"{id:N}.jpg"))) throw new IOException("Snapshot rendering failed.");
    }
}
