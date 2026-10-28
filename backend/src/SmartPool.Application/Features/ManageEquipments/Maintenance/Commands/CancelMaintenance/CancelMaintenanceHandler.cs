using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CancelMaintenance;

public sealed class CancelMaintenanceHandler(IMaintenanceOperations operations)
    : IRequestHandler<CancelMaintenanceCommand, Unit>
{
    public async Task<Unit> Handle(CancelMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await operations.ChangeStateAsync(command.Id, "cancel", cancellationToken);
        return Unit.Value;
    }
}
