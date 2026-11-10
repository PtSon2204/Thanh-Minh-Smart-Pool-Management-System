using MediatR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record VideoJobDto(Guid Id, string OriginalName, string Status, double Progress, string? Error, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record UploadVideoCommand(Stream Content, string FileName, long Length) : IRequest<VideoJobDto>;
public sealed record GetVideoJobQuery(Guid Id) : IRequest<VideoJobDto>;
public sealed record GetVideoFileQuery(Guid Id) : IRequest<string>;
public sealed record VideoPlaybackDto(string StreamPath, DateTimeOffset ExpiresAt);
public sealed record VideoPlaybackTicket(string Value, DateTimeOffset ExpiresAt);
public sealed record GetVideoPlaybackQuery(Guid Id, bool Original = false) : IRequest<VideoPlaybackDto>;
public sealed record GetVideoStreamQuery(Guid Id, string Ticket, bool Original = false) : IRequest<string>;
public interface IVideoPlaybackTickets
{
    VideoPlaybackTicket Create(Guid jobId);
    bool Validate(Guid jobId, string ticket);
}
public sealed record GetMonitoringStatusQuery : IRequest<object>;
public sealed record GetCameraAnalysisQuery(Guid Id) : IRequest<CameraAnalysisDto>;
public sealed record GetCameraAnalysisFrameQuery(Guid Id, string FrameId) : IRequest<byte[]?>;
public sealed record CameraAnalysisDto(string State, string? FrameId, DateTimeOffset? ObservedAt, DateTimeOffset? ProcessedAt,
    IReadOnlyList<RiskDecision> Risks, string Synchronization = "PAIRED_FRAME", IReadOnlyList<TrackedDetection>? Detections = null)
{
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;
    public Guid? CameraId { get; init; }
    public double? InferenceMilliseconds { get; init; }
}

public interface IVideoAnalysisService
{
    Task<VideoJobDto> UploadAsync(UploadVideoCommand command, Guid userId, CancellationToken ct);
    Task<VideoJobDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<string> FileAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    // Phát song song với phân tích: đang QUEUED/PROCESSING/RENDERING thì trả video gốc, COMPLETED thì trả video đã vẽ khung.
    Task<string> LiveFileAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<string> OriginalFileAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    object Status();
}
public interface ICameraAnalysisReader
{
    CameraAnalysisDto Get(Guid id);
    byte[]? Frame(Guid id, string frameId);
}
// Decisions (box cảnh báo) của frame gần nhất theo giây trong video đang phân tích — cho overlay
// khi phát song song, khớp 1:1 với video đã vẽ khung khi hoàn tất.
public sealed record VideoFrameAnalysisDto(double? AnalyzedAt, IReadOnlyList<RiskDecision> Risks);
public sealed record GetVideoFrameAnalysisQuery(Guid JobId, double Seconds) : IRequest<VideoFrameAnalysisDto>;
public sealed record VideoFrameAnalysisWindowDto(double Fps, int StartFrame, int ReadyFrames, bool Complete,
    IReadOnlyList<RiskDecision[]> Frames, bool Legacy = false);
public sealed record GetVideoFrameAnalysisWindowQuery(Guid JobId, double From, double Duration = 4) : IRequest<VideoFrameAnalysisWindowDto>;
public interface IVideoFrameAnalysisReader
{
    Task<VideoFrameAnalysisDto> Query(Guid jobId, double seconds, CancellationToken ct);
    Task<VideoFrameAnalysisWindowDto> WindowAsync(Guid jobId, double from, double duration, CancellationToken ct);
}
public sealed class GetVideoFrameAnalysisWindowHandler(IVideoFrameAnalysisReader reader, IVideoAnalysisService service, ICurrentUser user)
    : IRequestHandler<GetVideoFrameAnalysisWindowQuery, VideoFrameAnalysisWindowDto>
{
    public async Task<VideoFrameAnalysisWindowDto> Handle(GetVideoFrameAnalysisWindowQuery request, CancellationToken ct)
    {
        if (!double.IsFinite(request.From) || request.From is < 0 or > 86400 ||
            !double.IsFinite(request.Duration) || request.Duration is <= 0 or > 5)
            throw new ArgumentException("Khoảng phân tích video không hợp lệ.");
        var job = await service.GetAsync(request.JobId, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
        var window = await reader.WindowAsync(request.JobId, request.From, request.Duration, ct);
        return window with { Legacy = window.Fps == 0 && job.Status == "COMPLETED" };
    }
}
public sealed class GetVideoFrameAnalysisHandler(IVideoFrameAnalysisReader reader, IVideoAnalysisService service, ICurrentUser user)
    : IRequestHandler<GetVideoFrameAnalysisQuery, VideoFrameAnalysisDto>
{
    public async Task<VideoFrameAnalysisDto> Handle(GetVideoFrameAnalysisQuery request, CancellationToken ct)
    {
        if (request.Seconds < 0 || !double.IsFinite(request.Seconds)) throw new ArgumentException("Vị trí phát không hợp lệ.");
        // Kiểm tra quyền job trước khi đọc phân tích (admin thấy mọi job, người khác chỉ job mình tải lên).
        await service.GetAsync(request.JobId, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
        return await reader.Query(request.JobId, request.Seconds, ct);
    }
}
public sealed class UploadVideoHandler(IVideoAnalysisService service, ICurrentUser user) : IRequestHandler<UploadVideoCommand, VideoJobDto>
{
    public Task<VideoJobDto> Handle(UploadVideoCommand request, CancellationToken ct) => service.UploadAsync(request,
        user.UserId ?? throw new UnauthorizedAccessException(), ct);
}
public sealed class GetVideoJobHandler(IVideoAnalysisService service, ICurrentUser user) : IRequestHandler<GetVideoJobQuery, VideoJobDto>
{
    public Task<VideoJobDto> Handle(GetVideoJobQuery request, CancellationToken ct) => service.GetAsync(request.Id,
        user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
}
public sealed class GetVideoFileHandler(IVideoAnalysisService service, ICurrentUser user) : IRequestHandler<GetVideoFileQuery, string>
{
    public Task<string> Handle(GetVideoFileQuery request, CancellationToken ct) => service.FileAsync(request.Id,
        user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
}
public sealed class GetVideoPlaybackHandler(IVideoAnalysisService service, ICurrentUser user, IVideoPlaybackTickets tickets)
    : IRequestHandler<GetVideoPlaybackQuery, VideoPlaybackDto>
{
    public async Task<VideoPlaybackDto> Handle(GetVideoPlaybackQuery request, CancellationToken ct)
    {
        // Cho phát từ khi video gốc đã tải xong (QUEUED trở đi) — không phải chờ kết quả vẽ khung.
        if (request.Original)
            await service.OriginalFileAsync(request.Id, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
        else
            await service.LiveFileAsync(request.Id, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
        var ticket = tickets.Create(request.Id);
        return new($"/api/video-analyses/{request.Id}/stream?ticket={Uri.EscapeDataString(ticket.Value)}{(request.Original ? "&original=true" : "")}", ticket.ExpiresAt);
    }
}
public sealed class GetVideoStreamHandler(IVideoAnalysisService service, IVideoPlaybackTickets tickets)
    : IRequestHandler<GetVideoStreamQuery, string>
{
    public Task<string> Handle(GetVideoStreamQuery request, CancellationToken ct)
    {
        if (!tickets.Validate(request.Id, request.Ticket)) throw new UnauthorizedAccessException();
        // Ticket chỉ phát hành sau khi đã kiểm tra quyền; trong lúc phân tích đang chạy thì stream video gốc,
        // khi kết quả vẽ khung sẵn sàng thì stream kết quả.
        return request.Original ? service.OriginalFileAsync(request.Id, Guid.Empty, true, ct) : service.LiveFileAsync(request.Id, Guid.Empty, true, ct);
    }
}
public sealed class GetMonitoringStatusHandler(IVideoAnalysisService service) : IRequestHandler<GetMonitoringStatusQuery, object>
{
    public Task<object> Handle(GetMonitoringStatusQuery request, CancellationToken ct) => Task.FromResult(service.Status());
}
public sealed class GetCameraAnalysisHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisQuery, CameraAnalysisDto>
{
    public Task<CameraAnalysisDto> Handle(GetCameraAnalysisQuery request, CancellationToken ct) => Task.FromResult(reader.Get(request.Id));
}
public sealed class GetCameraAnalysisFrameHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisFrameQuery, byte[]?>
{
    public Task<byte[]?> Handle(GetCameraAnalysisFrameQuery request, CancellationToken ct) => Task.FromResult(reader.Frame(request.Id, request.FrameId));
}
