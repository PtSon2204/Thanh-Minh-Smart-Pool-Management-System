using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.SafetyMonitoring;

public interface ISafetyMonitoringService
{
    Task<PagedResponse<AlertDto>> ListAsync(GetAlertsQuery query, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
    Task<AlertDto> HandleAsync(Guid id, string action, Guid userId, bool admin, CancellationToken ct, string? expectedSeverity = null);
    Task<RiskWriteResult> RecordAsync(RecordRiskCommand command, CancellationToken ct);
    Task<Guid?> PrepareSnapshotAsync(RecordRiskCommand command, CancellationToken ct);
    Task CompleteSnapshotAsync(Guid id, string url, CancellationToken ct);
}
