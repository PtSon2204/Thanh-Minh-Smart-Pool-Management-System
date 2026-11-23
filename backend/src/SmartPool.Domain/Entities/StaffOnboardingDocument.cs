namespace SmartPool.Domain.Entities;

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
