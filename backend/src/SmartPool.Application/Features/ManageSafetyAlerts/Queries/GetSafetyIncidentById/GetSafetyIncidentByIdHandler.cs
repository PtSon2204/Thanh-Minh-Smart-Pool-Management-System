using MediatR;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidentById;

public sealed class GetSafetyIncidentByIdHandler(
    IRepository<Incident> incidents,
    IRepository<User> users,
    IRepository<UserProfile> profiles) : IRequestHandler<GetSafetyIncidentByIdQuery, SafetyIncidentDto>
{
    public async Task<SafetyIncidentDto> Handle(GetSafetyIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự cố.");

        var user = incident.ReportedBy.HasValue
            ? await users.GetByIdAsync(incident.ReportedBy.Value, cancellationToken)
            : null;
        var profile = incident.ReportedBy.HasValue
            ? await profiles.GetByIdAsync(incident.ReportedBy.Value, cancellationToken)
            : null;

        return SafetyIncidentMapper.ToDto(incident, user, profile);
    }
}
