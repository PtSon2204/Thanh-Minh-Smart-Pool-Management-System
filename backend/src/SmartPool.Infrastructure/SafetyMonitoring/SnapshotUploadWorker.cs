using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class SnapshotUploadWorker(IServiceScopeFactory scopes, MonitoringFiles files, AlertSnapshotQueue outbox, Cloudinary cloudinary, IOptions<MonitoringOptions> options,
    ILogger<SnapshotUploadWorker> logger) : BackgroundService
{
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

    private async Task RunLegacyAsync(CancellationToken stoppingToken)
    {
        var nextLegacy = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Chỉ còn phục hồi các bản ghi cũ từ phiên bản lưu trước khi có ảnh.
                if (DateTimeOffset.UtcNow < nextLegacy) { await Task.Delay(1000, stoppingToken); continue; }
                nextLegacy = DateTimeOffset.UtcNow.AddSeconds(10);
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var stale = DateTime.UtcNow.AddMinutes(-2);
                var interrupted = await db.Warnings.AsNoTracking().Where(x => x.SnapshotStatus == "CAPTURING" && x.UpdatedAt < stale)
                    .Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in interrupted)
                {
                    var available = File.Exists(files.PathFor($"{id:N}-source.jpg")) && File.Exists(files.PathFor($"{id:N}-boxes.json"));
                    await db.Warnings.Where(x => x.Id == id && x.SnapshotStatus == "CAPTURING")
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, available ? "PENDING" : "FAILED"), stoppingToken);
                }
                await db.Warnings.Where(x => x.SnapshotStatus == "UPLOADING" && x.UpdatedAt < stale)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, x => x.SnapshotAttempts >= 3 ? "FAILED" : "PENDING"), stoppingToken);
                var pending = await db.Warnings.AsNoTracking().Where(x => x.SnapshotStatus == "PENDING" && x.SnapshotAttempts < 3)
                    .OrderBy(x => x.WarningTime).Select(x => x.Id).Take(5).ToListAsync(stoppingToken);
                foreach (var id in pending)
                {
                    // Claim one attempt atomically across API instances; public_id is deterministic for retries.
                    var attempts = await db.Warnings.Where(x => x.Id == id && x.SnapshotStatus == "PENDING" && x.SnapshotAttempts < 3)
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotAttempts, x => x.SnapshotAttempts + 1)
                            .SetProperty(x => x.SnapshotStatus, "UPLOADING").SetProperty(x => x.UpdatedAt, DateTime.UtcNow), stoppingToken);
                    if (attempts == 0) continue;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(30));
                        if (!File.Exists(files.PathFor($"{id:N}.jpg"))) await RenderAsync(id, timeout.Token);
                        await using var image = File.OpenRead(files.PathFor($"{id:N}.jpg"));
                        var uploaded = await cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription($"{id:N}.jpg", image),
                            Folder = "smart-pool/incidents", PublicId = id.ToString("N"), Overwrite = true }, timeout.Token);
                        if (uploaded.Error != null || uploaded.SecureUrl == null) throw new IOException("Snapshot upload failed.");
                        await scope.ServiceProvider.GetRequiredService<ISafetyMonitoringService>().CompleteSnapshotAsync(id, uploaded.SecureUrl.AbsoluteUri, stoppingToken);
                        File.Delete(files.PathFor($"{id:N}.jpg"));
                        File.Delete(files.PathFor($"{id:N}-source.jpg"));
                        File.Delete(files.PathFor($"{id:N}-boxes.json"));
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        await db.Warnings.Where(x => x.Id == id && string.IsNullOrEmpty(x.ImageUrl))
                            .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, x => x.SnapshotAttempts >= 3 ? "FAILED" : "PENDING"), stoppingToken);
                        logger.LogWarning("Snapshot upload failed for alert {AlertId}. At most three attempts are permitted.", id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Snapshot queue unavailable. Retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
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
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                if (string.IsNullOrWhiteSpace(job.Command.ImageUrl))
                {
                    // Cố định public_id trước upload để retry không nhân bản asset.
                    job = job with { Command = job.Command with { AlertId = target } };
                    await outbox.SaveAsync(job, ct);
                    if (!File.Exists(files.PathFor($"{job.Id:N}.jpg"))) await RenderAsync(job.Id, timeout.Token);
                    await using var image = File.OpenRead(files.PathFor($"{job.Id:N}.jpg"));
                    var uploaded = await cloudinary.UploadAsync(new ImageUploadParams {
                        File = new FileDescription($"{job.Id:N}.jpg", image), Folder = "smart-pool/incidents",
                        // A concurrent ACK may require a new warning ID after this upload.
                        // Keep this observation's asset separate from the handled warning's photo.
                        PublicId = job.Id.ToString("N"), Overwrite = true }, timeout.Token);
                    if (uploaded.Error != null || uploaded.SecureUrl == null) throw new IOException("Snapshot upload failed.");
                    job = job with { Command = job.Command with { ImageUrl = uploaded.SecureUrl.AbsoluteUri } };
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
