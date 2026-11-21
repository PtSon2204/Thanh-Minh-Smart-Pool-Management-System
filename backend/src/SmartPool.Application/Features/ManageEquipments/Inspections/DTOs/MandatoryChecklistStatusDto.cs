using System;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed record MandatoryChecklistStatusDto
{
    public bool HasPendingMandatoryChecklist { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? ShiftId { get; init; }
    public string? ShiftName { get; init; }
    public string? InspectionWindow { get; init; }
    public string? TimeRange { get; init; }
    public string? AllowedFromTime { get; init; }
    public string? AllowedToTime { get; init; }
    public long? TaskRevision { get; init; }
    public string? ServerTime { get; init; }
}
