namespace SmartPool.Application.Features.ManageWaterQuality.Dtos;

public sealed class WaterQualityLogDto
{
    public Guid Id { get; set; }
    public DateTime InspectionTime { get; set; }
    public decimal PhValue { get; set; }
    public decimal FreeChlorineMgL { get; set; }
    public bool? IsWaterClear { get; set; }
    public string Source { get; set; } = "Manual";
    public Guid? InspectedBy { get; set; }
    public string? InspectorName { get; set; }
    public Guid? EquipmentId { get; set; }
    public string? EquipmentName { get; set; }
    public string Status { get; set; } = "NotEvaluated";
    public string? Recommendation { get; set; }
    public string? EvaluationVersion { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
