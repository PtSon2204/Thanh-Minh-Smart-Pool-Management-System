using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class VideoAnalysisService(SmartPoolDbContext db, MonitoringFiles files, VideoProbe probe,
    IOptions<MonitoringOptions> options, IOptions<CameraOptions> cameras) : IVideoAnalysisService
{
    public object Status() => new { aiConfigured = options.Value.Configured,
        uploadConfigured = options.Value.Configured && File.Exists(options.Value.FfprobePath) && File.Exists(cameras.Value.FfmpegPath),
        maxVideoMegabytes = options.Value.MaxVideoMegabytes, maxVideoSeconds = options.Value.MaxVideoSeconds,
        synchronization = "PAIRED_FRAME", liveSampling = "shared-camera-keyframes", maxLiveCameras = options.Value.MaxLiveCameras };

    public async Task<VideoJobDto> UploadAsync(UploadVideoCommand command, Guid userId, CancellationToken ct)
    {
        if (!options.Value.Configured || !File.Exists(options.Value.FfprobePath) || !File.Exists(cameras.Value.FfmpegPath))
            throw new InvalidOperationException("Dịch vụ phân tích video chưa được cấu hình đầy đủ.");
        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        if (extension is not (".mp4" or ".mov" or ".avi" or ".mkv") || command.Length <= 0 || command.Length > options.Value.MaxVideoMegabytes * 1024L * 1024)
            throw new ArgumentException($"Chọn MP4, MOV, AVI hoặc MKV, tối đa {options.Value.MaxVideoMegabytes} MB.");
        var id = Guid.NewGuid(); var reference = $"{id:N}{extension}"; var path = files.PathFor(reference);
        try
        {
            // Limit concurrent uploads before writing large files; the transaction only reserves a durable job.
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(782341250013)", ct);
                var globalCount = await db.VideoAnalysisJobs.CountAsync(x => x.Status == "UPLOADING" || x.Status == "QUEUED" || x.Status == "PROCESSING" || x.Status == "RENDERING", ct);
                if (globalCount >= options.Value.MaxQueuedJobs) throw new MonitoringConflictException("Hàng đợi phân tích đang đầy. Vui lòng thử lại sau.");
                var key = BitConverter.ToInt64(SHA256.HashData(userId.ToByteArray()), 0);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
                var count = await db.VideoAnalysisJobs.CountAsync(x => x.UploadedBy == userId &&
                    (x.Status == "UPLOADING" || x.Status == "QUEUED" || x.Status == "PROCESSING" || x.Status == "RENDERING"), ct);
                if (count >= options.Value.MaxActiveJobsPerUser) throw new MonitoringConflictException("Đã đạt giới hạn video đang chờ hoặc xử lý.");
                db.VideoAnalysisJobs.Add(new() { Id = id, UploadedBy = userId, OriginalName = Path.GetFileName(command.FileName)[..Math.Min(Path.GetFileName(command.FileName).Length, 255)],
                    InputFile = reference, Status = "UPLOADING", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            }
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await command.Content.ReadAsync(buffer, ct)) != 0)
                {
                    total += read;
                    if (total > options.Value.MaxVideoMegabytes * 1024L * 1024) throw new ArgumentException("Video vượt quá giới hạn dung lượng.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (total != command.Length) throw new ArgumentException("Dung lượng video không hợp lệ.");
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await probe.InspectAsync(path, false, timeout.Token); // Only metadata in HTTP; decoding and timeline checks run in the job.
            await db.VideoAnalysisJobs.Where(x => x.Id == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "QUEUED")
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            return await GetAsync(id, userId, true, ct);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            await db.VideoAnalysisJobs.Where(x => x.Id == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "FAILED")
                .SetProperty(x => x.Error, "Video chưa được tải lên hoặc kiểm tra thành công."), CancellationToken.None);
            throw;
        }
    }
    private Task<VideoAnalysisJob?> Find(Guid id, Guid userId, bool admin, CancellationToken ct) => db.VideoAnalysisJobs.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && (admin || x.UploadedBy == userId), ct);
    public async Task<VideoJobDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct)
    {
        var job = await Find(id, userId, admin, ct) ?? throw new KeyNotFoundException();
        return new(job.Id, job.OriginalName, job.Status, job.Progress, job.Error, job.CreatedAt, job.UpdatedAt);
    }
    public async Task<string> FileAsync(Guid id, Guid userId, bool admin, CancellationToken ct)
    {
        var job = await Find(id, userId, admin, ct) ?? throw new KeyNotFoundException();
        if (job.Status != "COMPLETED" || job.OutputFile == null) throw new MonitoringConflictException("Video kết quả chưa sẵn sàng.");
        var path = files.PathFor(job.OutputFile);
        if (!File.Exists(path)) throw new KeyNotFoundException();
        return path;
    }
}
