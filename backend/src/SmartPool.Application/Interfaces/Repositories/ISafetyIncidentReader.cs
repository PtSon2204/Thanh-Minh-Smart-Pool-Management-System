using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ISafetyIncidentReader
{
    Task<GetSafetyIncidentsResult> SearchAsync(GetSafetyIncidentsQuery query, CancellationToken ct);
}
