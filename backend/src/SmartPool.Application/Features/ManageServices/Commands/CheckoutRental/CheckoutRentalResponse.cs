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
}
