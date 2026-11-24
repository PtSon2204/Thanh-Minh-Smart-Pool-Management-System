using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Recovery for alerts persisted before the image-backed outbox was introduced.
public sealed partial class SnapshotUploadWorker
{
    private const int LegacyMaxAttempts = 3;
    private static readonly TimeSpan LegacyScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LegacyLeaseTimeout = TimeSpan.FromMinutes(2);

    private async Task RunLegacyAsync(CancellationToken stoppingToken)
    {
        var nextLegacy = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Chỉ còn phục hồi các bản ghi cũ từ phiên bản lưu trước khi có ảnh.
                if (DateTimeOffset.UtcNow < nextLegacy) { await Task.Delay(1000, stoppingToken); continue; }
                nextLegacy = DateTimeOffset.UtcNow.Add(LegacyScanInterval);
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var stale = DateTime.UtcNow.Subtract(LegacyLeaseTimeout);
                var interrupted = await db.Warnings.AsNoTracking().Where(x => x.SnapshotStatus == "CAPTURING" && x.UpdatedAt < stale)
                    .Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in interrupted)
                {
                    var available = File.Exists(files.PathFor($"{id:N}-source.jpg")) && File.Exists(files.PathFor($"{id:N}-boxes.json"));
                    await db.Warnings.Where(x => x.Id == id && x.SnapshotStatus == "CAPTURING")
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, available ? "PENDING" : "FAILED"), stoppingToken);
                }
                await db.Warnings.Where(x => x.SnapshotStatus == "UPLOADING" && x.UpdatedAt < stale)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, x => x.SnapshotAttempts >= LegacyMaxAttempts ? "FAILED" : "PENDING"), stoppingToken);
                var pending = await db.Warnings.AsNoTracking().Where(x => x.SnapshotStatus == "PENDING" && x.SnapshotAttempts < LegacyMaxAttempts)
                    .OrderBy(x => x.WarningTime).Select(x => x.Id).Take(5).ToListAsync(stoppingToken);
                foreach (var id in pending)
                {
                    // Claim one attempt atomically across API instances; public_id is deterministic for retries.
                    var attempts = await db.Warnings.Where(x => x.Id == id && x.SnapshotStatus == "PENDING" && x.SnapshotAttempts < LegacyMaxAttempts)
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotAttempts, x => x.SnapshotAttempts + 1)
                            .SetProperty(x => x.SnapshotStatus, "UPLOADING").SetProperty(x => x.UpdatedAt, DateTime.UtcNow), stoppingToken);
                    if (attempts == 0) continue;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(UploadTimeout);
                        var imageUrl = await UploadSnapshotAsync(id, timeout.Token);
                        await scope.ServiceProvider.GetRequiredService<ISafetyMonitoringService>()
                            .CompleteSnapshotAsync(id, imageUrl, stoppingToken);
                        File.Delete(files.PathFor($"{id:N}.jpg"));
                        File.Delete(files.PathFor($"{id:N}-source.jpg"));
                        File.Delete(files.PathFor($"{id:N}-boxes.json"));
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        await db.Warnings.Where(x => x.Id == id && string.IsNullOrEmpty(x.ImageUrl))
                            .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, x => x.SnapshotAttempts >= LegacyMaxAttempts ? "FAILED" : "PENDING"), stoppingToken);
                        logger.LogWarning("Snapshot upload failed for alert {AlertId}. At most three attempts are permitted.", id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Snapshot queue unavailable. Retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
