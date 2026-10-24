namespace SmartPool.Domain.Entities;

public class SwimClass
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int Capacity { get; set; }

    // Coach assigned to the class by default. A session may have a different coach.
    public Guid CoachUserId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User CoachUser { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}
