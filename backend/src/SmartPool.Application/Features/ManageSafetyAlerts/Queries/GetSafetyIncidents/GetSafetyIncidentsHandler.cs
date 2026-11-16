using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed class GetSafetyIncidentsHandler(ISafetyIncidentReader reader) : IRequestHandler<GetSafetyIncidentsQuery, GetSafetyIncidentsResult>
{
    public Task<GetSafetyIncidentsResult> Handle(GetSafetyIncidentsQuery request, CancellationToken ct)
    {
        if (request.PageNumber is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.SourceType is not (null or "camera" or "lifeguard") || request.FromDate > request.ToDate)
            throw new ArgumentException("Bộ lọc lịch sử sự cố không hợp lệ.");
        return reader.SearchAsync(request, ct);
    }
}
