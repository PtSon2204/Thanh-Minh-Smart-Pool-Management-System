using MediatR;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidentById;

public sealed class GetSafetyIncidentByIdHandler(
    ISafetyAlertOperations operations) : IRequestHandler<GetSafetyIncidentByIdQuery, SafetyIncidentDto>
{
    public async Task<SafetyIncidentDto> Handle(GetSafetyIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await operations.GetIncidentByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự cố.");

        var user = incident.ReportedBy.HasValue
            ? await operations.GetUserByIdAsync(incident.ReportedBy.Value, cancellationToken)
            : null;
        var profile = incident.ReportedBy.HasValue
            ? await operations.GetProfileByUserIdAsync(incident.ReportedBy.Value, cancellationToken)
            : null;

        return SafetyIncidentMapper.ToDto(incident, user, profile);
    }
}
