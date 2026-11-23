namespace SmartPool.Domain.Entities;

public sealed class StaffOnboardingRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? InvitationId { get; set; }
    public Guid? EmployeeId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string State { get; set; } = "DRAFT";
    public string ProtectedProfile { get; set; } = "";
    public string? CccdDigest { get; set; }
    public string? ReservedCccdDigest { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewerId { get; set; }
    public string? CorrectionReason { get; set; }
    public bool DocumentsProvided { get; set; }
    public long Version { get; set; } = 1;
}
