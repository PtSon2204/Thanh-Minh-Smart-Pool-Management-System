using MediatR;

namespace SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

public sealed class GetRentalDetailsQuery : IRequest<GetRentalDetailsResponse>
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
}
