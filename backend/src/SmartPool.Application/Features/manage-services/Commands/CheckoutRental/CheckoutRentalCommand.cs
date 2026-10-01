using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalCommand : IRequest<CheckoutRentalResponse>
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal DepositPerUnit { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }
}
