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
    IOptions<MonitoringOptions> options, IOptions<RiskOptions> risk, AlertSnapshotQueue snapshots, ILogger<LiveMonitoringWorker> logger,
    CameraDiagnostics? diagnostics = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)//overide ghi đè phương thức ExecuteAsync của BackgroundService để thực hiện công việc giám sát trực tiếp
    {
        var workers = new Dictionary<Guid, (CancellationTokenSource Stop, Task Run)>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                    var available = options.Value.Configured
                        ? await db.Equipments.AsNoTracking().Where(x => x.IsDeleted != true && x.Status == "Active" && x.Category.Trim().ToLower() == "camera")
                            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(stoppingToken)
                        : [];
                    var ids = LiveCameraSelection.Select(available.Select(id => (id, cameras.GetStatus(id))),
                        workers.Keys.ToHashSet(), options.Value.MaxLiveCameras, DateTimeOffset.UtcNow,
                        TimeSpan.FromSeconds(Math.Clamp(options.Value.LiveReconnectGraceSeconds, 1, 60)));
                    foreach (var id in workers.Keys.Except(ids).ToArray())
                    {
                        var worker = workers[id];
                        worker.Stop.Cancel();
                        state.Remove(id); // Không giữ metadata cũ trong lúc Windows đóng process GPU.
                        await worker.Run;
                        worker.Stop.Dispose(); workers.Remove(id); state.Remove(id);
                    }
                    foreach (var id in ids.Except(workers.Keys))
                    {
                        var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        workers.Add(id, (stop, RunCameraAsync(id, stop.Token)));
                    }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                { logger.LogWarning("Cannot refresh live camera workers; retrying discovery."); }
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            foreach (var worker in workers.Values) worker.Stop.Cancel();
            try { await Task.WhenAll(workers.Values.Select(x => x.Run)); }
            finally { foreach (var worker in workers.Values) worker.Stop.Dispose(); }
        }
    }

    // Mỗi camera có worker/tracker riêng: inference và IO của camera chậm không chặn camera khác.
    // MaxLiveCameras giới hạn số model resident; không tạo thêm kết nối RTSP.
    private async Task RunCameraAsync(Guid cameraId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!options.Value.Configured) { await Task.Delay(5000, stoppingToken); continue; }
                if (cameras.GetStatus(cameraId).State != "Connected" || frames.Capture(cameraId) == null)
                { state.Remove(cameraId); await Task.Delay(100, stoppingToken); continue; }
                var threads = Math.Max(1, options.Value.LiveInferenceThreads / Math.Clamp(options.Value.MaxLiveCameras, 1, 8));
                await using var ai = new AiWorkerClient(options.Value, inferenceThreads: threads);
                using (var startup = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                { startup.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds)); await ai.ReadyAsync(startup.Token); }
                // Fresh trackers after process restart, and therefore fresh engine episodes.
                var engine = new RiskDecisionEngine(risk.Value);
                var overlay = new LiveRiskOverlay();
                DateTimeOffset? lastObserved = null;
                string? sourceSession = null, oldTracks = null, oldRisks = null;
                var nextQueueWarning = DateTimeOffset.MinValue;
                var workerSession = Guid.NewGuid().ToString("N");
                logger.LogInformation("Live AI worker ready: {WorkerSession}. Tracking and risk episodes start a new session.", workerSession);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var analysisStart = Stopwatch.GetTimestamp();
                    var id = cameraId;
                    var status = cameras.GetStatus(id);
                    var captured = frames.Capture(id);
                    if (status.State != "Connected" || captured == null)
                    { state.Remove(id); await Task.Delay(100, stoppingToken); continue; }
                    var timestamp = captured.CapturedAt;
                    if (lastObserved >= timestamp) { await Task.Delay(10, stoppingToken); continue; }
                    var resetTracker = status.AnalysisSession != null && sourceSession != status.AnalysisSession;
                    if (resetTracker)
                    {
                        // Một lượt video simulator = một RTSP session; không mang track/window qua lượt mới.
                        sourceSession = status.AnalysisSession;
                        logger.LogInformation("Camera {CameraId} simulator session changed to {CameraSession}; resetting its tracking and risk window.", id, status.AnalysisSession);
                        engine = new RiskDecisionEngine(risk.Value);
                        overlay.Clear();
                        state.Remove(id);
                    }
                    lastObserved = timestamp;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);//timeout để hủy bỏ tác vụ nếu quá thời gian chờ
                    timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds));
                    var inferenceStart = Stopwatch.GetTimestamp();//bắt đầu đo thời gian inference
                    using var result = await ai.InferAsync(id, captured.Jpeg, resetTracker, timeout.Token);
                    var inferenceMs = Stopwatch.GetElapsedTime(inferenceStart).TotalMilliseconds;
                    var detections = AiWorkerClient.Detections(result);
                    // Ignore stale inference rather than painting old boxes or claiming current safety.
                    var currentStatus = cameras.GetStatus(id);
                    if (currentStatus.State != "Connected" || currentStatus.AnalysisSession != status.AnalysisSession ||
                        DateTimeOffset.UtcNow - timestamp > TimeSpan.FromSeconds(6))
                    { state.Remove(id); await Task.Delay(10, stoppingToken); continue; }
                    var observedSeconds = timestamp.ToUnixTimeMilliseconds() / 1000.0;
                    var decisions = engine.Observe(observedSeconds, detections);
                    var activeTrackIds = engine.ActiveTrackIds();
                    var display = overlay.Update(observedSeconds, detections, decisions, activeTrackIds);
                    if (diagnostics?.ShouldSampleFrame(id, DateTimeOffset.UtcNow) == true)
                    {
                        diagnostics.Record(new CameraDiagnosticEvent(DateTimeOffset.UtcNow, "camera_analysis_frame", id,
                            captured.FrameId, captured.RtpTimestamp, timestamp, inferenceMs, detections.Length,
                            detections.Select(d => new DiagnosticDetection(d.TrackId, d.Class, d.Confidence, d.Box)).Take(64).ToArray(),
                            activeTrackIds.Take(64).ToArray(),
                            display.Rendered.Select(d => new DiagnosticBox(d.TrackId, d.Episode, d.Severity, d.Box)).Take(64).ToArray(),
                            display.Current.Select(d => new DiagnosticBox(d.TrackId, d.Episode, d.Severity, d.Box)).Take(64).ToArray(),
                            decisions.Count, display.Rendered.Count, status.AnalysisSession, workerSession,
                            RenderMilliseconds: null, AnalysisMilliseconds: null));
                    }
                    // Chỉ log lúc thay đổi, để phân biệt đổi ID với đổi severity trong cùng episode.
                    // Không ghi frame, URL RTSP hay thông tin đăng nhập vào log.
                    var tracks = string.Join(",", detections.Select(d => d.TrackId).Distinct().OrderBy(value => value, StringComparer.Ordinal));
                    if (oldTracks != tracks)
                    {
                        logger.LogInformation("Camera {CameraId} tracks changed: [{PreviousTracks}] -> [{Tracks}] at {ObservedAt}.", id, oldTracks, tracks, timestamp);
                        oldTracks = tracks;
                    }
                    var risks = string.Join(",", decisions.OrderBy(d => d.TrackId, StringComparer.Ordinal).Select(d => $"{d.TrackId}:{d.Severity}:{d.Episode}"));
                    if (oldRisks != risks)
                    {
                        logger.LogInformation("Camera {CameraId} active risk boxes changed: [{PreviousRisks}] -> [{Risks}] at {ObservedAt}. Empty means no current risk box, not confirmed recovery.", id, oldRisks, risks, timestamp);
                        oldRisks = risks;
                    }
                    // Paired viewers need the same latched geometry as the canvas
                    // viewers, even when confidence is below the evidence threshold.
                    var renderStart = Stopwatch.GetTimestamp();
                    var annotated = display.Rendered.Count == 0 ? captured.Jpeg : await ai.RenderAsync(display.Rendered, timeout.Token);
                    var renderMs = display.Rendered.Count == 0 ? 0 : Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
                    stoppingToken.ThrowIfCancellationRequested();
                    state.Set(id, captured.FrameId, timestamp, decisions, annotated, detections, inferenceMs, captured.RtpTimestamp,
                        workerSession + ":" + (status.AnalysisSession ?? "camera"), activeTrackIds, renderMs,
                        Stopwatch.GetElapsedTime(analysisStart).TotalMilliseconds, display.Current);
                    // Video được phát ngay; broadcaster riêng gửi metadata mới nhất.
                    // Client chậm/SignalR không được giữ vòng inference.
                    foreach (var decision in decisions)
                    {
                        try
                        {
                            var accepted = snapshots.TryStage(new RecordRiskCommand(id, decision,
                                detections.First(x => x.TrackId == decision.TrackId).Confidence, timestamp.UtcDateTime), captured.Jpeg, annotated);
                            if (!accepted && DateTimeOffset.UtcNow >= nextQueueWarning)
                            {
                                logger.LogWarning("Camera {CameraId} snapshot refused by bounded queue ({RejectedSnapshots} total); later observations will retry.", id, snapshots.RejectedSnapshots);
                                nextQueueWarning = DateTimeOffset.UtcNow.AddSeconds(15);
                            }
                        }
                        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                        { logger.LogWarning("Cannot stage snapshot for camera {CameraId} ({FailureType}); no imageless alert was saved.", id, exception.GetType().Name); }
                    }
                    // Inference reads the newest decoded frame; do not add a quarter-second pause per cycle.
                    await Task.Delay(10, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                state.Remove(cameraId);
                logger.LogWarning("Live AI monitoring unavailable ({FailureType}). Restarting worker and tracking sessions; no risk is classified as safe.", exception.GetType().Name);
                try { await Task.Delay(1000, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
