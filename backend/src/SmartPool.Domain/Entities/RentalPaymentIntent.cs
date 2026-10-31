namespace SmartPool.Domain.Entities;

public sealed class RentalPaymentIntent
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LinesJson { get; set; } = string.Empty;
    public decimal FeeAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public Guid? IssuedByUserId { get; set; }
}
