namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class DailyInspectionItemDto
{
    public Guid EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsPassed { get; set; } = true;
    public string? Note { get; set; }
}
