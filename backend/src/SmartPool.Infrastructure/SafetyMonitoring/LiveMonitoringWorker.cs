using MediatR;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class LiveMonitoringWorker(IServiceScopeFactory scopes, ICameraService cameras, ICameraFrameSource frames, CameraAnalysisState state,
    IOptions<MonitoringOptions> options, IOptions<RiskOptions> risk, MonitoringFiles files, ILogger<LiveMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var engines = new Dictionary<Guid, RiskDecisionEngine>();
        var last = new Dictionary<Guid, DateTimeOffset>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!options.Value.Configured) { await Task.Delay(5000, stoppingToken); continue; }
                await using var ai = new AiWorkerClient(options.Value);
                using (var startup = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                { startup.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds)); await ai.ReadyAsync(startup.Token); }
                // Fresh trackers after process restart, and therefore fresh engine episodes.
                engines.Clear(); last.Clear();
                var ids = new List<Guid>();
                var refreshAt = DateTimeOffset.MinValue;
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                    if (DateTimeOffset.UtcNow >= refreshAt)
                    {
                        ids = await db.Equipments.AsNoTracking().Where(x => x.IsDeleted != true && x.Status == "Active" && x.Category.Trim().ToLower() == "camera")
                            .OrderBy(x => x.Id).Select(x => x.Id).Take(options.Value.MaxLiveCameras).ToListAsync(stoppingToken);
                        refreshAt = DateTimeOffset.UtcNow.AddSeconds(1);
                    }
                    foreach (var id in engines.Keys.Except(ids).ToArray()) { engines.Remove(id); last.Remove(id); state.Remove(id); }
                    foreach (var id in ids)
                    {
                        var status = cameras.GetStatus(id);
                        var captured = frames.Capture(id);
                        if (status.State != "Connected" || captured == null) { state.Remove(id); continue; }
                        var timestamp = captured.CapturedAt;
                        if (last.TryGetValue(id, out var previous) && previous >= timestamp) continue;
                        last[id] = timestamp;
                        if (!engines.TryGetValue(id, out var engine)) engines[id] = engine = new RiskDecisionEngine(risk.Value);
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds));
                        var inferenceStart = Stopwatch.GetTimestamp();
                        using var result = await ai.SendAsync(new { op = "infer", key = id.ToString(), jpeg = Convert.ToBase64String(captured.Jpeg) }, timeout.Token);
                        var inferenceMs = Stopwatch.GetElapsedTime(inferenceStart).TotalMilliseconds;
                        var detections = AiWorkerClient.Detections(result);
                        // Ignore stale inference rather than painting old boxes or claiming current safety.
                        if (DateTimeOffset.UtcNow - timestamp > TimeSpan.FromSeconds(6)) { state.Remove(id); continue; }
                        var decisions = engine.Observe(timestamp.ToUnixTimeMilliseconds() / 1000.0, detections);
                        using var rendered = await ai.SendAsync(new { op = "render", decisions }, timeout.Token);
                        var annotated = Convert.FromBase64String(rendered.RootElement.GetProperty("jpeg").GetString()!);
                        state.Set(id, captured.FrameId, timestamp, decisions, annotated, detections, inferenceMs);
                        // Deliver even an empty risks array immediately: it clears old boxes.
                        // Do not wait for PostgreSQL / snapshot IO before updating live viewers.
                        try
                        {
                            await scope.ServiceProvider.GetRequiredService<INotificationService>().SendToGroupAsync(
                                $"camera-analysis_{id:N}", "camera_analysis", state.Get(id), stoppingToken);
                        }
                        catch (Exception) when (!stoppingToken.IsCancellationRequested)
                        {
                            logger.LogDebug("Camera analysis push unavailable for {CameraId}; clients recover through polling.", id);
                        }
                        foreach (var decision in decisions)
                        {
                            var write = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordRiskCommand("LIVE", id, decision,
                                detections.First(x => x.TrackId == decision.TrackId).Confidence, timestamp.UtcDateTime), timeout.Token);
                            if (write.SnapshotRequired)
                            {
                                // Stage the immutable CRITICAL source before the next frame; upload/render run independently.
                                var alertId = write.Alert.Id;
                                await File.WriteAllBytesAsync(files.PathFor($"{alertId:N}-source.jpg"), captured.Jpeg, stoppingToken);
                                await File.WriteAllTextAsync(files.PathFor($"{alertId:N}-boxes.json"), System.Text.Json.JsonSerializer.Serialize(new[] { decision },
                                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), stoppingToken);
                                await db.Warnings.Where(x => x.Id == alertId).ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, "PENDING"), stoppingToken);
                            }
                        }
                    }
                    // Inference reads the newest decoded frame; do not add a quarter-second pause per cycle.
                    await Task.Delay(10, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                foreach (var id in engines.Keys) state.Remove(id);
                logger.LogWarning("Live AI monitoring unavailable. Retrying; no risk is classified as safe.");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
