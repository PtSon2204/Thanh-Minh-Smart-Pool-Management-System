namespace SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory
{
    public sealed class GetInventoryHistoryResponse
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
