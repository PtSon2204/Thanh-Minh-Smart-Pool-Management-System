namespace SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory
{
    public sealed class GetInventoryHistoryResponse
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ReceiptDetailId { get; set; }
        public Guid? ReceiptId { get; set; }
        public string? ReceiptCode { get; set; }
        public decimal? UnitCost { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
