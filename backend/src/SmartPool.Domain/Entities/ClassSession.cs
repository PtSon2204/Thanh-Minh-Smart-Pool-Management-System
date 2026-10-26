namespace SmartPool.Domain.Entities;

public class ClassSession
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public Guid CoachUserId { get; set; }

    // Store session times as UTC values.
    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual SwimClass Class { get; set; } = null!;

    public virtual User CoachUser { get; set; } = null!;

    public virtual ICollection<ClassAttendance> Attendances { get; set; } = new List<ClassAttendance>();
}
