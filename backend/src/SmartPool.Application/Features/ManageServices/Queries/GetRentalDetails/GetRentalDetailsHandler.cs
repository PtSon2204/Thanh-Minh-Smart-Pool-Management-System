using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;

public sealed class GetRentalDetailsHandler : IRequestHandler<GetRentalDetailsQuery, GetRentalDetailsResponse>
{
    private readonly IServiceOperations _operations;

    public GetRentalDetailsHandler(IServiceOperations operations) => _operations = operations;

    public Task<GetRentalDetailsResponse> Handle(GetRentalDetailsQuery request, CancellationToken cancellationToken) =>
        _operations.GetRentalDetailsAsync(request, cancellationToken);
}
