using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.UpdateMaintenanceSchedule;

public sealed class UpdateMaintenanceScheduleHandler(IMaintenanceOperations operations)
    : IRequestHandler<UpdateMaintenanceScheduleCommand, MaintenanceItem>
{
    public Task<MaintenanceItem> Handle(UpdateMaintenanceScheduleCommand command, CancellationToken cancellationToken)
    {
        var request = new MaintenanceRequest
        {
            EquipmentId = command.EquipmentId,
            TaskName = command.TaskName,
            RepeatUnit = command.RepeatUnit,
            RepeatEvery = command.RepeatEvery,
            StartDate = command.StartDate,
            AssignedTo = command.AssignedTo,
            RemindBeforeDays = command.RemindBeforeDays
        };
        return operations.UpdateAsync(command.Id, request, cancellationToken);
    }
}
