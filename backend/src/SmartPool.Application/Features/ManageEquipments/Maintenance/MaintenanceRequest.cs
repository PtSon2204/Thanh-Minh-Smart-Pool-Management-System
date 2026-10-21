using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceRequest
{
    public Guid EquipmentId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string RepeatUnit { get; set; } = "None";
    public int RepeatEvery { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public Guid AssignedTo { get; set; }
    public int RemindBeforeDays { get; set; } = 3;
}
