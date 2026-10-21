using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;

public sealed class ReturnRentalQuantityCommand : IRequest<ReturnRentalQuantityResponse>
{
    [JsonIgnore]
    public Guid OrderId { get; set; }

    [JsonIgnore]
    public Guid ProductId { get; set; }

    [JsonIgnore]
    public Guid OperatorId { get; set; }

    public List<Guid>? RentalIds { get; set; }
}
