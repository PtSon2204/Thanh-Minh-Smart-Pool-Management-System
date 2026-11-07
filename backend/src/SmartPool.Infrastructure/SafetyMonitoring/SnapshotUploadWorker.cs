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

public sealed class SnapshotUploadWorker(IServiceScopeFactory scopes, MonitoringFiles files, Cloudinary cloudinary, IOptions<MonitoringOptions> options,
    ILogger<SnapshotUploadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var stale = DateTime.UtcNow.AddMinutes(-2);
                var interrupted = await db.Warnings.AsNoTracking().Where(x => x.SnapshotStatus == "CAPTURING" && x.UpdatedAt < stale).Select(x => x.Id).ToListAsync(stoppingToken);
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
                        await scope.ServiceProvider.GetRequiredService<IMonitoringRepository>().CompleteSnapshotAsync(id, uploaded.SecureUrl.AbsoluteUri, stoppingToken);
                        File.Delete(files.PathFor($"{id:N}.jpg"));
                        File.Delete(files.PathFor($"{id:N}-source.jpg"));
                        File.Delete(files.PathFor($"{id:N}-boxes.json"));
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        await db.Warnings.Where(x => x.Id == id && x.ImageUrl == null)
                            .ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, x => x.SnapshotAttempts >= 3 ? "FAILED" : "PENDING"), stoppingToken);
                        logger.LogWarning("Snapshot upload failed for alert {AlertId}. At most three attempts are permitted.", id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Snapshot queue unavailable. Retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
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
