namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalResponse
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public Guid OrderDetailId { get; set; }
        public List<CheckoutRentalItemResponse> Rentals { get; set; } = new List<CheckoutRentalItemResponse>();
    }

    public sealed class CheckoutRentalItemResponse
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
