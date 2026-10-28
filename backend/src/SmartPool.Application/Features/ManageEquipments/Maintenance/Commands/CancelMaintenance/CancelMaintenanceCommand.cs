using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CancelMaintenance;

public sealed record CancelMaintenanceCommand(Guid Id) : IRequest<Unit>;
