using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record GetAlertsQuery(Guid? CameraId = null,
    string? Status = null, int Page = 1, int PageSize = 20, string? Area = null,
    string? Severity = null, DateTimeOffset? FromTime = null, DateTimeOffset? ToTime = null) : IRequest<PagedResponse<AlertDto>>;

public sealed record GetAlertFiltersQuery : IRequest<AlertFilterOptions>;

public sealed record GetAlertQuery(Guid Id) : IRequest<AlertDto>;

public sealed record HandleAlertCommand(Guid Id, string Action, string? ExpectedSeverity = null) : IRequest<AlertDto>;

public sealed record RecordRiskCommand(Guid CameraId, RiskDecision Decision, double Confidence,
    DateTime ObservedAt, string? ImageUrl = null, Guid? AlertId = null) : IRequest<RiskWriteResult>;
