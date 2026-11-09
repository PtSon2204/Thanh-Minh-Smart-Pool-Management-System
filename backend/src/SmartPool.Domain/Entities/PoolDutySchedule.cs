namespace SmartPool.Domain.Entities;

public class PoolDutySchedule
{
    public Guid Id { get; set; }

    public DateOnly DutyDate { get; set; }

    public Guid ShiftId { get; set; }

    public Guid? AssignedEmployeeId { get; set; }

    // Scheduled, InProgress, Completed, Absent, Cancelled
    public string Status { get; set; } = "Scheduled";

    // Auto hoặc Manual
    public string AssignmentType { get; set; } = "Auto";

    public Guid? WaterQualityLogId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Shift? Shift { get; set; }

    public virtual User? AssignedEmployee { get; set; }

    public virtual WaterQualityLog? WaterQualityLog { get; set; }
}

