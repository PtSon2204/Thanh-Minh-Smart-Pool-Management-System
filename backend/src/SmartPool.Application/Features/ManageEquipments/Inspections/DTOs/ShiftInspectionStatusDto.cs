using System;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class ShiftInspectionStatusDto
{
    public Guid ShiftId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string AllowedFromTime { get; set; } = string.Empty;
    public string AllowedToTime { get; set; } = string.Empty;
    public bool IsOpenNow { get; set; }
    public bool IsAlreadySubmittedToday { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedByName { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
