using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.StartMaintenance;

public sealed class StartMaintenanceHandler(IMaintenanceOperations operations)
    : IRequestHandler<StartMaintenanceCommand, Unit>
{
    public async Task<Unit> Handle(StartMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await operations.ChangeStateAsync(command.Id, "start", cancellationToken);
        return Unit.Value;
    }
}
