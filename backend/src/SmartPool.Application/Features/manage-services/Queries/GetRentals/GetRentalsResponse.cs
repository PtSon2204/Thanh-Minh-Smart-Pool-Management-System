namespace SmartPool.Application.Features.ManageServices.Queries.GetRentals
{
    public sealed class GetRentalsResponse
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public DateTime? RentTime { get; set; }
        public DateTime? ReturnTime { get; set; }
        public decimal? DepositAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? OrderStatus { get; set; }
    }
}
