namespace SmartPool.Domain.Entities;

public class PoolDutySchedule
{
    public Guid Id { get; set; }

    public DateOnly DutyDate { get; set; }

    public Guid? ShiftId { get; set; }

    public Guid? EmployeeShiftId { get; set; }

    public string? TaskType { get; set; }

    public string? InspectionWindow { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? DueNoticeSentAtUtc { get; set; }

    public Guid? AssignedEmployeeId { get; set; }

    public Guid? PreferredEmployeeId { get; set; }

    public Guid? MeasurementRecurrenceId { get; set; }

    public Guid? TriggerWaterQualityLogId { get; set; }

    public string? UnassignedReason { get; set; }

    public long Revision { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    // Scheduled, InProgress, Completed, Absent, Cancelled
    public string Status { get; set; } = "Scheduled";

    // Auto hoặc Manual
    public string AssignmentType { get; set; } = "Auto";

    public Guid? WaterQualityLogId { get; set; }

    public Guid? LastExecutedBy { get; set; }

    public DateTime? LastExecutedAtUtc { get; set; }

    public Guid? LastExecutionAssigneeId { get; set; }

    public bool? LastExecutionWasAdminOverride { get; set; }

    public Guid? DailyInspectionId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Shift? Shift { get; set; }

    public virtual User? AssignedEmployee { get; set; }

    public virtual WaterQualityLog? WaterQualityLog { get; set; }
}

public static class PoolDutyTaskTypes
{
    public const string Measurement = "Measurement";
    public const string EquipmentChecklist = "EquipmentChecklist";
    public const string Cleaning = "Cleaning";
}

public static class PoolDutyInspectionWindows
{
    public const string Morning = "MORNING";
    public const string Afternoon = "AFTERNOON";
}

