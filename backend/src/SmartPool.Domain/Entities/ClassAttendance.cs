namespace SmartPool.Domain.Entities;

public class ClassAttendance
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid RegistrationId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid SubmittedByUserId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ClassSession Session { get; set; } = null!;

    public virtual ClassRegistration Registration { get; set; } = null!;

    public virtual User SubmittedByUser { get; set; } = null!;

    public virtual User? ReviewedByUser { get; set; }
}
