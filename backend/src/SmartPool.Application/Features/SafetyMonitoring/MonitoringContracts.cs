using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record AlertDto(Guid Id, string SourceType, Guid? CameraId, Guid? VideoJobId, string? CameraName,
    string? Location, string? TrackId, string? RiskEpisode, string Severity, string Status, double? VideoTimestamp,
    string? ImageUrl, string SnapshotStatus, Guid? IncidentId, Guid? HandledBy, DateTime? HandledAt, DateTime CreatedAt,
    double[]? Box = null)
{
    public DateTime? ObservedAt { get; init; }
}
public sealed record GetAlertsQuery(string? SourceType = null, Guid? CameraId = null, Guid? JobId = null,
    string? Status = null, int Page = 1, int PageSize = 20) : IRequest<PagedResponse<AlertDto>>;
public sealed record GetAlertQuery(Guid Id) : IRequest<AlertDto>;
public sealed record HandleAlertCommand(Guid Id, string Action, string? ExpectedSeverity = null) : IRequest<AlertDto>;
public sealed record RecordRiskCommand(string SourceType, Guid SourceId, RiskDecision Decision, double Confidence,
    DateTime ObservedAt, string? ImageUrl = null, Guid? AlertId = null) : IRequest<RiskWriteResult>;
public sealed record RiskWriteResult(AlertDto Alert);

public sealed class MonitoringConflictException(string message, AlertDto? currentAlert = null) : Exception(message)
{
    public AlertDto? CurrentAlert { get; } = currentAlert;
}
public interface IMonitoringRepository
{
    Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<Warning?> FindEpisodeAsync(string source, string prefix, CancellationToken ct);
    // Infrastructure owns atomic persistence and lock ordering. The callback is
    // an Application use case, executed against the locked, current entity.
    Task<T> WithAlertAsync<T>(Guid id, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct);
    Task<T> WithEpisodeAsync<T>(string source, string prefix, Func<IMonitoringWriteSession, CancellationToken, Task<T>> change, CancellationToken ct);
}

public interface IMonitoringWriteSession
{
    Warning? Current { get; }
    Task<bool> IsVideoOwnerAsync(Guid jobId, Guid userId, CancellationToken ct);
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
            request.SourceType is not (null or "LIVE" or "UPLOAD") ||
            request.Status is not (null or "PENDING" or "ACKNOWLEDGED" or "CANCELLED")) throw new ArgumentException("Bộ lọc không hợp lệ.");
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
