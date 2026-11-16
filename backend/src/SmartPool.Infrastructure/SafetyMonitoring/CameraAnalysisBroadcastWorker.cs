using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.SafetyMonitoring;

// Metadata phục vụ tag/canvas, không nằm trên đường phát video. Coalesce về frame mới nhất.
public sealed class CameraAnalysisBroadcastWorker(CameraAnalysisState state, IServiceScopeFactory scopes,
    ILogger<CameraAnalysisBroadcastWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sent = new System.Collections.Concurrent.ConcurrentDictionary<Guid, string>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var current = state.Active();
                foreach (var old in sent.Keys.Except(current.Select(x => x.CameraId!.Value)).ToArray()) sent.TryRemove(old, out _);
                if (current.Length == 0) { await Task.Delay(200, stoppingToken); continue; }
                await Parallel.ForEachAsync(current, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = stoppingToken }, async (frame, ct) =>
                {
                    var id = frame.CameraId!.Value;
                    if (frame.FrameId == null || sent.GetValueOrDefault(id) == frame.FrameId) return;
                    using var scope = scopes.CreateScope();
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(200);
                    try
                    {
                        await notifications.SendToGroupAsync($"camera-analysis_{id:N}", "camera_analysis", frame, timeout.Token);
                        sent[id] = frame.FrameId;
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogDebug("Camera metadata push deferred for {CameraId}; video continues independently.", id); }
                });
                await Task.Delay(75, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
