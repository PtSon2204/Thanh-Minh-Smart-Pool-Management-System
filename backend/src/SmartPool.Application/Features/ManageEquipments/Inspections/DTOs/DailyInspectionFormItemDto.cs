namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class DailyInspectionFormItemDto
{
    public Guid EquipmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
}
