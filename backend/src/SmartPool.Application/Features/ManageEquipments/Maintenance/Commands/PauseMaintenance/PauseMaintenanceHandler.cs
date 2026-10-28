using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.PauseMaintenance;

public sealed class PauseMaintenanceHandler(IMaintenanceOperations operations)
    : IRequestHandler<PauseMaintenanceCommand, Unit>
{
    public async Task<Unit> Handle(PauseMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await operations.ChangeStateAsync(command.Id, "pause", cancellationToken);
        return Unit.Value;
    }
}
