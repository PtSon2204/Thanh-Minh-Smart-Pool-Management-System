namespace SmartPool.Application.Features.ManageWaterQuality.Dtos;

public sealed class PoolDutyScheduleDto
{
    public Guid Id { get; set; }
    public DateOnly DutyDate { get; set; }
    public Guid? ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly ShiftStartTime { get; set; }
    public TimeOnly ShiftEndTime { get; set; }
    public string? TaskType { get; set; }
    public string? InspectionWindow { get; set; }
    public Guid? EmployeeShiftId { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public Guid? MeasurementRecurrenceId { get; set; }
    public Guid? TriggerWaterQualityLogId { get; set; }
    public string? UnassignedReason { get; set; }
    public long Revision { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public Guid? PreferredEmployeeId { get; set; }
    public string AssignedEmployeeName { get; set; } = string.Empty;
    public string? AssignedEmployeeEmail { get; set; }
    public string? EmployeeRole { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string AssignmentType { get; set; } = "Auto";
    public Guid? WaterQualityLogId { get; set; }
    public Guid? DailyInspectionId { get; set; }
    public string? DailyInspectionStatus { get; set; }
    public DateTime? DailyInspectionAt { get; set; }
    public decimal? MeasuredPh { get; set; }
    public decimal? MeasuredChlorine { get; set; }
    public string? MeasurementStatus { get; set; }
    public DateTime? MeasuredAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsTrendDuty { get; set; }
    public string? TrendSummary { get; set; }
    public List<string> SuggestedChemicals { get; set; } = new();
}

public sealed class PoolDutyStaffOptionDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public sealed class PoolDutyShiftOptionDto
{
    public Guid ShiftId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class PoolDutyOptionsResponse
{
    public List<PoolDutyStaffOptionDto> Staffs { get; set; } = [];
    public List<PoolDutyShiftOptionDto> Shifts { get; set; } = [];
}

public sealed record PoolDutyMeasurementRecurrenceDto(Guid Id, DateOnly AnchorDate, DateOnly NextOccurrenceDate,
    TimeOnly ScheduledTime, Guid ShiftId, string ShiftName, Guid PreferredEmployeeId, string PreferredEmployeeName,
    bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record OperationalTaskWeekConfigurationDto(Guid Id, DateOnly WeekStart, int StartDayOfWeek,
    TimeOnly ScheduledTime, Guid ShiftId, Guid PreferredEmployeeId);

public sealed class PoolDutyEligibleEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid EmployeeShiftId { get; set; }
    public Guid ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly ShiftStartTime { get; set; }
    public TimeOnly ShiftEndTime { get; set; }
}

public sealed class AutoAssignPoolDutyResultDto
{
    public int TotalSlotsProcessed { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<PoolDutyScheduleDto> AssignedSchedules { get; set; } = [];
}

public sealed record TodayWaterQualityGateDto(
    bool RequiresMeasurement,
    DateOnly? DutyDate,
    Guid? TodayLogId,
    string? Reason);

