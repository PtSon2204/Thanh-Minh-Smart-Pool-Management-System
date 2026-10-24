namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalResponse
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public Guid RequestId { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public decimal RentalFeeTotal { get; set; }
        public decimal DepositTotal { get; set; }
        public decimal TotalCollected { get; set; }
        public List<CheckoutRentalItemResponse> Rentals { get; set; } = new List<CheckoutRentalItemResponse>();
    }
}
