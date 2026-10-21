namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalResponse
    {
        public ReturnRentalItemResponse Rental { get; set; } = new ReturnRentalItemResponse();
        public int StockQuantity { get; set; }
    }
}
