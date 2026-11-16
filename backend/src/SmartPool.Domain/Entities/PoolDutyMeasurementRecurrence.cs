namespace SmartPool.Domain.Entities;

public sealed class PoolDutyMeasurementRecurrence
{
    public Guid Id { get; set; }

    public DateOnly AnchorDate { get; set; }

    public DateOnly NextOccurrenceDate { get; set; }

    public TimeOnly ScheduledTime { get; set; }

    public Guid ShiftId { get; set; }

    public Guid PreferredEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
