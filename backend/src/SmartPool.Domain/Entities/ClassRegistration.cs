namespace SmartPool.Domain.Entities;

public class ClassRegistration
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public Guid CustomerUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RegisteredAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public string? CancelReason { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual SwimClass Class { get; set; } = null!;

    public virtual User CustomerUser { get; set; } = null!;

    public virtual ICollection<ClassAttendance> Attendances { get; set; } = new List<ClassAttendance>();
}
