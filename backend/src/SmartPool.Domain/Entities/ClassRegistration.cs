namespace SmartPool.Domain.Entities;

public class ClassRegistration
{
    public Guid Id { get; set; }

    // Registration is for one session, not the whole swim class.
    public Guid SessionId { get; set; }

    public Guid CustomerUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ClassSession Session { get; set; } = null!;

    public virtual User CustomerUser { get; set; } = null!;

    public virtual User? ReviewedByUser { get; set; }

    public virtual ClassAttendance? Attendance { get; set; }
}
