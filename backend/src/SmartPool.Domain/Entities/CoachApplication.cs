namespace SmartPool.Domain.Entities;

public sealed class CoachApplication
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CertificatePublicId { get; set; } = null!;
    public string CertificateFormat { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTime SubmittedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }

    public User User { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public User? RevokedByUser { get; set; }
}
