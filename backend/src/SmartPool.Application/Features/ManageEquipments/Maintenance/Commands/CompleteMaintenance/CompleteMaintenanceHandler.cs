using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CompleteMaintenance;

public sealed class CompleteMaintenanceHandler(IMaintenanceOperations operations)
    : IRequestHandler<CompleteMaintenanceCommand, Unit>
{
    public async Task<Unit> Handle(CompleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var request = new CompleteMaintenanceRequest
        {
            Cost = command.Cost,
            VendorName = command.VendorName,
            Note = command.Note,
            ReadyForUse = command.ReadyForUse
        };
        await operations.CompleteAsync(command.Id, request, command.PerformedBy, cancellationToken);
        return Unit.Value;
    }
}
