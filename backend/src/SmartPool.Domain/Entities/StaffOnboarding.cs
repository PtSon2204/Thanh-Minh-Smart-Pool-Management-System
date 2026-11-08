namespace SmartPool.Domain.Entities;

public sealed class StaffInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public DateOnly JoinDate { get; set; }
    public string? ContractJson { get; set; }
    public string TokenDigest { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "PENDING";
    public string DeliveryStatus { get; set; } = "PENDING";
    public string? DeliveryErrorCode { get; set; }
    public Guid CreatedBy { get; set; }
    public string? ProtectedPasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CompletedUserId { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class StaffOnboardingRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? InvitationId { get; set; }
    public Guid? EmployeeId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string State { get; set; } = "DRAFT";
    public string ProtectedProfile { get; set; } = "";
    public string? CccdDigest { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewerId { get; set; }
    public string? CorrectionReason { get; set; }
    public bool DocumentsProvided { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class StaffOnboardingDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RevisionId { get; set; }
    public string Purpose { get; set; } = "";
    public string? AssetId { get; set; }
    public string? PublicId { get; set; }
    public string? AssetVersion { get; set; }
    public string ResourceType { get; set; } = "image";
    public string DeliveryType { get; set; } = "authenticated";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public Guid UploaderId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? UploadSettledAtUtc { get; set; }
    public DateTime? DeleteAfterUtc { get; set; }
    public string DeletionState { get; set; } = "AVAILABLE";
    public string? DeletionReason { get; set; }
    public DateTime? RetryAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
}

public sealed class StaffIdentityClaim
{
    public string Digest { get; set; } = "";
    public Guid OwnerId { get; set; }
}

public sealed class StaffCorrectionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = "";
    public string State { get; set; } = "REQUESTED";
    public DateTime CreatedAtUtc { get; set; }
    public Guid? RevisionId { get; set; }
    public long Version { get; set; } = 1;
}
