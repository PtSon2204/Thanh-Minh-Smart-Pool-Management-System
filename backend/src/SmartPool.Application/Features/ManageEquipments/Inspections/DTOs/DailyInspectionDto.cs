namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class DailyInspectionDto
{
    public Guid Id { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public Guid? ShiftId { get; set; }
    public DateOnly InspectionDate { get; set; }
    public DateTime InspectionTime { get; set; }
    public string OverallStatus { get; set; } = "Passed";
    public int TotalItems { get; set; }
    public int IssuesCount { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<DailyInspectionItemDto> Items { get; set; } = [];
}
