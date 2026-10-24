using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalCommand : IRequest<ReturnRentalResponse>
    {
        public Guid RequestId { get; set; }
        public string Condition { get; set; } = "Normal";
        public bool SettlementConfirmed { get; set; }
        public string? Reason { get; set; }
        [JsonIgnore]
        public Guid RentalId { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }
}
