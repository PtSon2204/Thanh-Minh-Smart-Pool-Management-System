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
