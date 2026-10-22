using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed record GetSafetyIncidentsResult
{
    public IReadOnlyList<SafetyIncidentDto> Items { get; init; } = new List<SafetyIncidentDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    // Summary counters
    public int TotalIncidents { get; init; }
    public int PendingCount { get; init; }
    public int InProgressCount { get; init; }
    public int ResolvedCount { get; init; }
    public int EmergencyCount { get; init; }
    public int CameraCount { get; init; }
    public int LifeguardCount { get; init; }
}
