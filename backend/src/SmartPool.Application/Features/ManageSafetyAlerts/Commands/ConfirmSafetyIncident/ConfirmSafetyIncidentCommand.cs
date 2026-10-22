using System;
using MediatR;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ConfirmSafetyIncident;

public sealed record ConfirmSafetyIncidentCommand : IRequest<ConfirmSafetyIncidentResult>
{
    public Guid IncidentId { get; init; }
    public string Status { get; init; } = "Resolved";
    public string? ResolutionNote { get; init; }
    public string ConfirmedByUserId { get; init; } = string.Empty;
    public string ConfirmedByName { get; init; } = string.Empty;
}
