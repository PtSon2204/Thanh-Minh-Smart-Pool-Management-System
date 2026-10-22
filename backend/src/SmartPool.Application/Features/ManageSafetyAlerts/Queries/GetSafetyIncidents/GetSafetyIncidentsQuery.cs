using MediatR;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed record GetSafetyIncidentsQuery : IRequest<GetSafetyIncidentsResult>
{
    public string? Search { get; init; }
    public string? Zone { get; init; }
    public string? SeverityLevel { get; init; }
    public string? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
