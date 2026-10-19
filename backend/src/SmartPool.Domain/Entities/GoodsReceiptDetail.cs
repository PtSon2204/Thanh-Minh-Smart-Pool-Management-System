namespace SmartPool.Domain.Entities;

public class GoodsReceiptDetail
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public virtual GoodsReceipt Receipt { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
