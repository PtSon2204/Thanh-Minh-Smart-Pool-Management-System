using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record AlertDto(Guid Id, Guid? CameraId, string? CameraName,
    string? Location, string? TrackId, string? RiskEpisode, string Severity, string Status,
    string? ImageUrl, string SnapshotStatus, Guid? IncidentId, Guid? HandledBy, DateTime? HandledAt, DateTime CreatedAt,
    double[]? Box = null)
{
    public DateTime? ObservedAt { get; init; }
    public string? HandledByName { get; init; }
}
public sealed record GetAlertsQuery(Guid? CameraId = null,
    string? Status = null, int Page = 1, int PageSize = 20, string? Area = null,
    string? Severity = null, DateTimeOffset? FromTime = null, DateTimeOffset? ToTime = null) : IRequest<PagedResponse<AlertDto>>;
public sealed record AlertCameraOption(Guid Id, string Name);
public sealed record AlertFilterOptions(string[] Areas, string[] Severities, string[] Statuses,
    AlertCameraOption[] Cameras, string[] Dates);
public sealed record GetAlertFiltersQuery : IRequest<AlertFilterOptions>;
public sealed class GetAlertFiltersHandler(IMonitoringRepository repository) : IRequestHandler<GetAlertFiltersQuery, AlertFilterOptions>
{
    public Task<AlertFilterOptions> Handle(GetAlertFiltersQuery request, CancellationToken ct) => repository.FiltersAsync(ct);
}
public sealed record GetAlertQuery(Guid Id) : IRequest<AlertDto>;
public sealed record HandleAlertCommand(Guid Id, string Action, string? ExpectedSeverity = null) : IRequest<AlertDto>;
public sealed record RecordRiskCommand(Guid CameraId, RiskDecision Decision, double Confidence,
    DateTime ObservedAt, string? ImageUrl = null, Guid? AlertId = null) : IRequest<RiskWriteResult>;
public sealed record RiskWriteResult(AlertDto Alert);

public sealed class MonitoringConflictException(string message, AlertDto? currentAlert = null) : Exception(message)
{
    public AlertDto? CurrentAlert { get; } = currentAlert;
}
public interface IMonitoringRepository
{
    Task<AlertFilterOptions> FiltersAsync(CancellationToken ct);
    Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<Warning?> FindEpisodeAsync(string prefix, CancellationToken ct);
    // Infrastructure owns atomic persistence and lock ordering. The callback is
    // an Application use case, executed against the locked, current entity.
    Task<T> WithAlertAsync<T>(Guid id, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct);
    Task<T> WithEpisodeAsync<T>(string prefix, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct);
}

public interface IMonitoringWriteSession
{
    Warning? Current { get; }
    Task<Equipment> GetCameraAsync(Guid id, CancellationToken ct);
    void AddWarning(Warning warning);
    void AddIncident(Incident incident);
    Task UpdateIncidentImageAsync(Guid incidentId, string imageUrl, CancellationToken ct);
}

public interface ISafetyMonitoringService
{
    Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> HandleAsync(Guid id, string action, Guid userId, bool admin, CancellationToken ct, string? expectedSeverity = null);
    Task<RiskWriteResult> RecordAsync(RecordRiskCommand command, CancellationToken ct);
    Task<Guid?> PrepareSnapshotAsync(RecordRiskCommand command, CancellationToken ct);
    Task CompleteSnapshotAsync(Guid id, string url, CancellationToken ct);
}

public sealed class GetAlertsHandler(ISafetyMonitoringService service, ICurrentUser user)
    : IRequestHandler<GetAlertsQuery, PagedResponse<AlertDto>>
{
    public Task<PagedResponse<AlertDto>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        if (request.Page is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.Status is not (null or "PENDING" or "ACKNOWLEDGED" or "CANCELLED") ||
            request.Severity is not (null or "WARNING" or "CRITICAL" or "EMERGENCY") || request.Area?.Length > 200 ||
            request.FromTime > request.ToTime) throw new ArgumentException("Bộ lọc không hợp lệ.");
        return service.ListAsync(request, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
    }
}
public sealed class GetAlertHandler(ISafetyMonitoringService service, ICurrentUser user) : IRequestHandler<GetAlertQuery, AlertDto>
{
    public Task<AlertDto> Handle(GetAlertQuery request, CancellationToken ct) => service.GetAsync(request.Id,
        user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
}
public sealed class HandleAlertHandler(ISafetyMonitoringService service, ICurrentUser user) : IRequestHandler<HandleAlertCommand, AlertDto>
{
    public Task<AlertDto> Handle(HandleAlertCommand request, CancellationToken ct)
    {
        if (request.Action is not ("ACKNOWLEDGED" or "CANCELLED")) throw new ArgumentException("Thao tác không hợp lệ.");
        if (request.ExpectedSeverity is not (null or "WARNING" or "CRITICAL" or "EMERGENCY")) throw new ArgumentException("Mức cảnh báo không hợp lệ.");
        return service.HandleAsync(request.Id, request.Action, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct, request.ExpectedSeverity);
    }
}
public sealed class RecordRiskHandler(ISafetyMonitoringService service) : IRequestHandler<RecordRiskCommand, RiskWriteResult>
{
    public Task<RiskWriteResult> Handle(RecordRiskCommand request, CancellationToken ct) => service.RecordAsync(request, ct);
}

// Live camera analysis queries and metadata.
public sealed record GetCameraAnalysisQuery(Guid Id) : IRequest<CameraAnalysisDto>;
public sealed record GetCameraAnalysisFrameQuery(Guid Id, string FrameId) : IRequest<byte[]?>;
public sealed record CameraAnalysisDto(string State, string? FrameId, DateTimeOffset? ObservedAt, DateTimeOffset? ProcessedAt,
    IReadOnlyList<RiskDecision> Risks, string Synchronization = "PAIRED_FRAME", IReadOnlyList<TrackedDetection>? Detections = null)
{
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;
    public Guid? CameraId { get; init; }
    public double? InferenceMilliseconds { get; init; }
    public double? RenderMilliseconds { get; init; }
    // Capture/AI/risk/render up to metadata publication; excludes upload and loop delay.
    public double? AnalysisMilliseconds { get; init; }
    public uint? RtpTimestamp { get; init; }
    public string? TrackingSession { get; init; }
    public IReadOnlyList<string>? ActiveRiskTrackIds { get; init; }
    // Current measured geometry with a latched risk colour, for new viewers.
    // Display-only; Risks remains the actual evidence from RiskDecisionEngine.
    public IReadOnlyList<RiskDecision>? DisplayRisks { get; init; }
}

public interface ICameraAnalysisReader
{
    CameraAnalysisDto Get(Guid id);
    byte[]? Frame(Guid id, string frameId);
}
public sealed class GetCameraAnalysisHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisQuery, CameraAnalysisDto>
{
    public Task<CameraAnalysisDto> Handle(GetCameraAnalysisQuery request, CancellationToken ct) => Task.FromResult(reader.Get(request.Id));
}
public sealed class GetCameraAnalysisFrameHandler(ICameraAnalysisReader reader) : IRequestHandler<GetCameraAnalysisFrameQuery, byte[]?>
{
    public Task<byte[]?> Handle(GetCameraAnalysisFrameQuery request, CancellationToken ct) => Task.FromResult(reader.Frame(request.Id, request.FrameId));
}
