using MediatR;

namespace SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

public sealed record GetIncidentStatisticsQuery : IRequest<IncidentStatisticsResponse>
{
    /// <summary>
    /// Bộ lọc sự cố: "all", "critical_urgent", "in_progress"
    /// </summary>
    public string? Filter { get; init; } = "all";
}
