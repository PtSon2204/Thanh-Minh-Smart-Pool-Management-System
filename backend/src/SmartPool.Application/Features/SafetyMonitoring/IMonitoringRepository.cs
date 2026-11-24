using SmartPool.Application.Common.Models;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.SafetyMonitoring;

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
