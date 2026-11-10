using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class VideoAnalysisWorker(IServiceScopeFactory scopes, MonitoringFiles files, VideoProbe probe,
    IOptions<MonitoringOptions> options, IOptions<CameraOptions> cameras, IOptions<RiskOptions> risk,
    VideoAnalysisState analysisState, ILogger<VideoAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One low-priority CPU worker. Never shares a Python inference session with live monitoring.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!options.Value.Configured) { await Task.Delay(5000, stoppingToken); continue; }
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
                var stale = DateTime.UtcNow.AddHours(-2);
                await db.VideoAnalysisJobs.Where(x => (x.Status == "PROCESSING" || x.Status == "RENDERING" || x.Status == "UPLOADING") && x.UpdatedAt < stale)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "FAILED").SetProperty(x => x.Error, "Tác vụ bị gián đoạn. Vui lòng tải lại video."), stoppingToken);
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var job = await db.VideoAnalysisJobs.FromSqlRaw("SELECT * FROM smart_pool.video_analysis_jobs WHERE status = 'QUEUED' ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED")
                    .SingleOrDefaultAsync(stoppingToken);
                if (job == null) { await tx.CommitAsync(stoppingToken); await Task.Delay(2000, stoppingToken); continue; }
                job.Status = "PROCESSING"; job.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(stoppingToken); await tx.CommitAsync(stoppingToken);
                var stage = "CHECK_VIDEO";
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromHours(2));
                    var ct = deadline.Token;
                    var input = files.PathFor(job.InputFile);
                    var info = await probe.InspectAsync(input, true, ct);
                    var intermediate = files.PathFor($"{job.Id:N}-render.avi");
                    stage = "START_AI";
                    await using (var ai = new AiWorkerClient(options.Value, true))
                    {
                        using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        startup.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds));
                        await ai.ReadyAsync(startup.Token);
                        var engine = new RiskDecisionEngine(risk.Value);
                        var lastProgress = DateTime.UtcNow;
                        // PHASE 1 — phân tích nhanh (infer thôi, không paint/encode/capture): AI chạy
                        // trước video (nhanh hơn real-time), nên box/cảnh báo khi phát song song không trễ.
                        // Snapshot (ảnh minh họa cảnh báo) tách sang PHASE 3 (SNAPSHOT) — offline,
                        // không còn chặn realtime.
                        var frameIndex = 0;
                        var recorded = new HashSet<(string Track, string Episode, string Severity)>();
                        var snapshotQueue = new List<(Guid AlertId, SmartPool.Application.Features.SafetyMonitoring.RiskDecision Decision, int FrameIndex)>();
                        analysisState.Prepare(job.Id, info.Fps, info.FrameCount ?? 0);
                        using var opened = await ai.SendAsync(new { op = "open", input, output = (string)null, fps = info.Fps, frames = info.FrameCount }, startup.Token);
                        while (true)
                        {
                            using var step = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            step.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds));
                            stage = "ANALYZE";
                            using var next = await ai.SendAsync(new { op = "next" }, step.Token);
                            if (next.RootElement.TryGetProperty("done", out _)) break;
                            var timestamp = next.RootElement.GetProperty("timestamp").GetDouble();
                            var detections = AiWorkerClient.Detections(next);
                            var decisions = engine.Observe(timestamp, detections);
                            // Lưu decisions của frame vào RAM để (a) video phát song song overlay box 1:1
                            // với render và (b) phase 2 vẽ khung đúng như video kết quả.
                            analysisState.SetFrame(job.Id, frameIndex, decisions);
                            stage = "SAVE_ALERT";
                            foreach (var decision in decisions)
                            {
                                // UPLOAD cannot re-arm a handled episode. Persist each severity transition
                                // once instead of waiting on a DB transaction for every video frame.
                                if (!recorded.Add((decision.TrackId, decision.Episode, decision.Severity))) continue;
                                var write = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordRiskCommand("UPLOAD", job.Id, decision,
                                    detections.First(x => x.TrackId == decision.TrackId).Confidence, job.CreatedAt.AddSeconds(timestamp)), step.Token);
                                // Chỉ ghi nhớ cảnh báo cần snapshot (hàng đã tạo với SnapshotStatus=CAPTURING).
                                // Chụp frame + ghi file + đặt PENDING thực hiện ở phase 3 (offline), không chặn realtime.
                                if (write.SnapshotRequired)
                                    snapshotQueue.Add((write.Alert.Id, decision, frameIndex));
                            }
                            frameIndex++;
                            if (DateTime.UtcNow - lastProgress > TimeSpan.FromSeconds(2))
                            {
                                stage = "SAVE_PROGRESS";
                                job.Progress = next.RootElement.GetProperty("progress").GetDouble() * 0.90;
                                job.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); lastProgress = DateTime.UtcNow;
                            }
                        }
                        stage = "SAVE_RESULT";
                        await analysisState.CompleteAsync(job.Id, ct);
                        // PHASE 2 — render offline: mở lại video, vẽ khung từ decisions đã lưu (đúng 1:1
                        // với cảnh báo phase 1), encode video kết quả. Không chặn realtime nữa.
                        stage = "PAINT";
                        using var rendered = await ai.SendAsync(new { op = "reopen_render", input, output = intermediate, fps = info.Fps, frames = info.FrameCount }, ct);
                        // Snapshot tách riêng: khi render qua đúng frame cảnh báo, chụp frame gốc (image
                        // chưa vẽ) + ghi file + đặt PENDING — offline, không ảnh hưởng pha phân tích.
                        // GroupBy (không ToDictionary) để 2+ cảnh báo cùng frame đều được chụp.
                        var snapshotByFrame = snapshotQueue.GroupBy(x => x.FrameIndex).ToDictionary(g => g.Key, g => (IReadOnlyList<(Guid AlertId, RiskDecision Decision)>)g.Select(x => (x.AlertId, x.Decision)).ToList());
                        var renderIndex = 0;
                        while (true)
                        {
                            using var step = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            step.CancelAfter(TimeSpan.FromSeconds(options.Value.WorkerTimeoutSeconds));
                            using var s = await ai.SendAsync(new { op = "step" }, step.Token);
                            if (s.RootElement.TryGetProperty("done", out _)) break;
                            if (snapshotByFrame.TryGetValue(renderIndex, out var snaps))
                            {
                                stage = "SAVE_SNAPSHOT";
                                // image hiện tại = frame gốc (chưa paint); capture chỉ encode JPEG, rẻ.
                                using var captured = await ai.SendAsync(new { op = "capture" }, step.Token);
                                var sourceBytes = Convert.FromBase64String(captured.RootElement.GetProperty("jpeg").GetString()!);
                                foreach (var (alertId, decision) in snaps)
                                {
                                    await File.WriteAllBytesAsync(files.PathFor($"{alertId:N}-source.jpg"), sourceBytes, step.Token);
                                    await File.WriteAllTextAsync(files.PathFor($"{alertId:N}-boxes.json"), System.Text.Json.JsonSerializer.Serialize(new[] { decision },
                                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), step.Token);
                                    await db.Warnings.Where(x => x.Id == alertId).ExecuteUpdateAsync(set => set.SetProperty(x => x.SnapshotStatus, "PENDING"), step.Token);
                                }
                            }
                            stage = "PAINT";
                            using var painted = await ai.SendAsync(new { op = "paint", decisions = analysisState.FrameAt(job.Id, renderIndex) }, step.Token);
                            renderIndex++;
                        }
                    }
                    job.Status = "RENDERING"; job.Progress = 0.90; job.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    var output = $"{job.Id:N}-result.mp4";
                    stage = "ENCODE";
                    await EncodeAsync(intermediate, input, files.PathFor(output), info.Duration, ct);
                    job.Status = "COMPLETED"; job.OutputFile = output; job.Progress = 1; job.UpdatedAt = DateTime.UtcNow;
                    stage = "SAVE_RESULT";
                    await db.SaveChangesAsync(ct); File.Delete(intermediate);
                    // Xong: video kết quả đã vẽ khung sẵn, không cần RAM frame decisions nữa.
                    analysisState.Remove(job.Id);
                }
                catch (Exception ex)
                {
                    var error = ex is ArgumentException && stage == "CHECK_VIDEO" ? ex.Message : stage switch
                    {
                        "CHECK_VIDEO" => "Không kiểm tra được video đầu vào. Vui lòng chọn video khác.",
                        "START_AI" => "Không khởi động được dịch vụ AI. Vui lòng thử lại sau.",
                        "SAVE_ALERT" or "SAVE_SNAPSHOT" or "SAVE_PROGRESS" or "SAVE_RESULT" => "Không lưu được kết quả phân tích. Vui lòng thử lại.",
                        "PAINT" or "ENCODE" => "Không tạo được video kết quả. Vui lòng thử lại.",
                        _ => "AI không xử lý được khung hình trong video. Vui lòng thử lại."
                    };
                    // A failed alert insert must not be retried by SaveChanges while recording job failure.
                    db.ChangeTracker.Clear();
                    await db.VideoAnalysisJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(set => set
                        .SetProperty(x => x.Status, "FAILED").SetProperty(x => x.Error, error)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
                    analysisState.Remove(job.Id);
                    var reason = ex.GetBaseException() is Npgsql.PostgresException postgres ? postgres.SqlState : ex.GetType().Name;
                    logger.LogWarning("Video analysis failed for job {JobId} at {Stage}: {Reason}.", job.Id, stage, reason);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Video analysis queue unavailable. Retrying."); await Task.Delay(5000, stoppingToken); }
        }
    }

    private async Task EncodeAsync(string intermediate, string original, string output, double duration, CancellationToken ct)
    {
        var start = new ProcessStartInfo(cameras.Value.FfmpegPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        // Encode video kết quả (bước cuối): nhiều thread libx264 để nhanh, không tranh với phân tích.
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", intermediate, "-i", original,
            "-map", "0:v:0", "-map", "1:a:0?", "-c:v", "libx264", "-preset", "veryfast", "-threads", "8", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-movflags", "+faststart", "-t", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Video renderer unavailable.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception) { } });
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); await error;
        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0) throw new IOException("Video rendering failed.");
    }
}
