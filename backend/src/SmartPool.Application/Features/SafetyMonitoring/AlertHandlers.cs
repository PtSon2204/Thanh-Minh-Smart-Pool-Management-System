using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed class GetAlertFiltersHandler(IMonitoringRepository repository) : IRequestHandler<GetAlertFiltersQuery, AlertFilterOptions>
{
    public Task<AlertFilterOptions> Handle(GetAlertFiltersQuery request, CancellationToken ct) => repository.FiltersAsync(ct);
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
