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
    ILogger<VideoAnalysisWorker> logger) : BackgroundService
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
                        using var opened = await ai.SendAsync(new { op = "open", input, output = intermediate, fps = info.Fps, frames = info.FrameCount }, startup.Token);
                        var engine = new RiskDecisionEngine(risk.Value);
                        var lastProgress = DateTime.UtcNow;
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
                            stage = "SAVE_ALERT";
                            foreach (var decision in decisions)
                                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordRiskCommand("UPLOAD", job.Id, decision,
                                    detections.First(x => x.TrackId == decision.TrackId).Confidence, job.CreatedAt.AddSeconds(timestamp)), step.Token);
                            stage = "PAINT";
                            using var painted = await ai.SendAsync(new { op = "paint", decisions }, step.Token);
                            if (DateTime.UtcNow - lastProgress > TimeSpan.FromSeconds(2))
                            {
                                stage = "SAVE_PROGRESS";
                                job.Progress = next.RootElement.GetProperty("progress").GetDouble() * 0.90;
                                job.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); lastProgress = DateTime.UtcNow;
                            }
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
                }
                catch (Exception ex)
                {
                    var error = ex is ArgumentException && stage == "CHECK_VIDEO" ? ex.Message : stage switch
                    {
                        "CHECK_VIDEO" => "Không kiểm tra được video đầu vào. Vui lòng chọn video khác.",
                        "START_AI" => "Không khởi động được dịch vụ AI. Vui lòng thử lại sau.",
                        "SAVE_ALERT" or "SAVE_PROGRESS" or "SAVE_RESULT" => "Không lưu được kết quả phân tích. Vui lòng thử lại.",
                        "PAINT" or "ENCODE" => "Không tạo được video kết quả. Vui lòng thử lại.",
                        _ => "AI không xử lý được khung hình trong video. Vui lòng thử lại."
                    };
                    // A failed alert insert must not be retried by SaveChanges while recording job failure.
                    db.ChangeTracker.Clear();
                    await db.VideoAnalysisJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(set => set
                        .SetProperty(x => x.Status, "FAILED").SetProperty(x => x.Error, error)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
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
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-threads", "1", "-i", intermediate, "-i", original,
            "-map", "0:v:0", "-map", "1:a:0?", "-c:v", "libx264", "-preset", "fast", "-threads", "1", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-movflags", "+faststart", "-t", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Video renderer unavailable.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception) { } });
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); await error;
        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0) throw new IOException("Video rendering failed.");
    }
}
