using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.ResumeMaintenance;

public sealed class ResumeMaintenanceHandler(IMaintenanceOperations operations)
    : IRequestHandler<ResumeMaintenanceCommand, Unit>
{
    public async Task<Unit> Handle(ResumeMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await operations.ChangeStateAsync(command.Id, "resume", cancellationToken);
        return Unit.Value;
    }
}
