using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CreateMaintenanceSchedule;

public sealed class CreateMaintenanceScheduleHandler(IMaintenanceOperations operations)
    : IRequestHandler<CreateMaintenanceScheduleCommand, MaintenanceItem>
{
    public Task<MaintenanceItem> Handle(CreateMaintenanceScheduleCommand command, CancellationToken cancellationToken)
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
        return operations.CreateAsync(request, cancellationToken);
    }
}
