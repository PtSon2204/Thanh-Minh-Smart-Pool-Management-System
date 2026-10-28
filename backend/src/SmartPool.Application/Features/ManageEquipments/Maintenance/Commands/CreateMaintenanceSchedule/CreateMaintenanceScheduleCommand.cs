using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CreateMaintenanceSchedule;

public sealed class CreateMaintenanceScheduleCommand : IRequest<MaintenanceItem>
{
    public Guid EquipmentId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string RepeatUnit { get; set; } = "None";
    public int RepeatEvery { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public Guid AssignedTo { get; set; }
    public int RemindBeforeDays { get; set; } = 3;
}
