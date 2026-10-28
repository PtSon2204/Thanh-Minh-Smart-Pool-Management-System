using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.PauseMaintenance;

public sealed record PauseMaintenanceCommand(Guid Id) : IRequest<Unit>;
