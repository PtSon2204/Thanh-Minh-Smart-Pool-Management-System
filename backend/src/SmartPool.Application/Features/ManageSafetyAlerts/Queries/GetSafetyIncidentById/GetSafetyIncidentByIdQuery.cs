using MediatR;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidentById;

public sealed record GetSafetyIncidentByIdQuery(Guid Id) : IRequest<SafetyIncidentDto>;
