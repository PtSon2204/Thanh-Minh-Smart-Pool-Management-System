using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalCommand : IRequest<ReturnRentalResponse>
    {
        [JsonIgnore]
        public Guid RentalId { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }
}
