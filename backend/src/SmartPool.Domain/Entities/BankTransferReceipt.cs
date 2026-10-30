namespace SmartPool.Domain.Entities;

public sealed class BankTransferReceipt
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public long ProviderEventId { get; set; }
    public string FinancialHash { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? BankReference { get; set; }
    public string Disposition { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool NeedsReconciliation { get; set; }
}
