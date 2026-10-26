namespace SmartPool.Domain.Entities;

public sealed class RentalTransaction
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? PaymentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public decimal FeeAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal RetainedAmount { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public string? Reason { get; set; }
    public string ResultJson { get; set; } = string.Empty;
}
