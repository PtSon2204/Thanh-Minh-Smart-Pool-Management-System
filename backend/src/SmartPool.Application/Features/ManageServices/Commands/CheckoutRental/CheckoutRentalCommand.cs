using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalCommand : IRequest<CheckoutRentalResponse>
    {
        public Guid RequestId { get; set; }
        public List<CheckoutRentalLine> Items { get; set; } = [];
        public bool CollectionConfirmed { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }

    public sealed class CheckoutRentalLine
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal ExpectedUnitPrice { get; set; }
    }
}
