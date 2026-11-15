using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record AlertDto(Guid Id, string SourceType, Guid? CameraId, Guid? VideoJobId, string? CameraName,
    string? Location, string? TrackId, string? RiskEpisode, string Severity, string Status, double? VideoTimestamp,
    string? ImageUrl, string SnapshotStatus, Guid? IncidentId, Guid? HandledBy, DateTime? HandledAt, DateTime CreatedAt,
    double[]? Box = null);
public sealed record GetAlertsQuery(string? SourceType = null, Guid? CameraId = null, Guid? JobId = null,
    string? Status = null, int Page = 1, int PageSize = 20) : IRequest<PagedResponse<AlertDto>>;
public sealed record GetAlertQuery(Guid Id) : IRequest<AlertDto>;
public sealed record HandleAlertCommand(Guid Id, string Action) : IRequest<AlertDto>;
public sealed record RecordRiskCommand(string SourceType, Guid SourceId, RiskDecision Decision, double Confidence,
    DateTime ObservedAt) : IRequest<RiskWriteResult>;
public sealed record RiskWriteResult(AlertDto Alert, bool SnapshotRequired);

public sealed class MonitoringConflictException(string message) : Exception(message);
public interface IMonitoringRepository
{
    Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> HandleAsync(Guid id, string action, Guid userId, bool admin, CancellationToken ct);
    Task<RiskWriteResult> RecordAsync(RecordRiskCommand command, CancellationToken ct);
    Task CompleteSnapshotAsync(Guid id, string url, CancellationToken ct);
}

public sealed class GetAlertsHandler(IMonitoringRepository repository, ICurrentUser user)
    : IRequestHandler<GetAlertsQuery, PagedResponse<AlertDto>>
{
    public Task<PagedResponse<AlertDto>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        if (request.Page is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.SourceType is not (null or "LIVE" or "UPLOAD") ||
            request.Status is not (null or "PENDING" or "ACKNOWLEDGED" or "CANCELLED")) throw new ArgumentException("Bộ lọc không hợp lệ.");
        return repository.ListAsync(request, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
    }
}
public sealed class GetAlertHandler(IMonitoringRepository repository, ICurrentUser user) : IRequestHandler<GetAlertQuery, AlertDto>
{
    public Task<AlertDto> Handle(GetAlertQuery request, CancellationToken ct) => repository.GetAsync(request.Id,
        user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
}
public sealed class HandleAlertHandler(IMonitoringRepository repository, ICurrentUser user) : IRequestHandler<HandleAlertCommand, AlertDto>
{
    public Task<AlertDto> Handle(HandleAlertCommand request, CancellationToken ct)
    {
        if (request.Action is not ("ACKNOWLEDGED" or "CANCELLED")) throw new ArgumentException("Thao tác không hợp lệ.");
        return repository.HandleAsync(request.Id, request.Action, user.UserId ?? throw new UnauthorizedAccessException(), user.IsInRole("ADMIN"), ct);
    }
}
public sealed class RecordRiskHandler(IMonitoringRepository repository) : IRequestHandler<RecordRiskCommand, RiskWriteResult>
{
    public Task<RiskWriteResult> Handle(RecordRiskCommand request, CancellationToken ct) => repository.RecordAsync(request, ct);
}
