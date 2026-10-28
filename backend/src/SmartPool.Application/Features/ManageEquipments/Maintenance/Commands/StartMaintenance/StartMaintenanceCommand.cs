using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.StartMaintenance;

public sealed record StartMaintenanceCommand(Guid Id) : IRequest<Unit>;
