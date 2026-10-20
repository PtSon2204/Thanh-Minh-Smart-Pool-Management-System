namespace SmartPool.Domain.Entities;

public class GoodsReceipt
{
    public Guid Id { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string? SupplierName { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid CreatedBy { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User Creator { get; set; } = null!;
    public virtual ICollection<GoodsReceiptDetail> Details { get; set; } = new List<GoodsReceiptDetail>();
}
