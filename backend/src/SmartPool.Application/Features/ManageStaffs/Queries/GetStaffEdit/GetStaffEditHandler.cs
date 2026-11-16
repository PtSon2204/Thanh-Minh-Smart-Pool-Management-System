using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;

public sealed class GetStaffEditHandler(IStaffOperations operations) : IRequestHandler<GetStaffEditQuery, GetStaffEditResponse>
{
    public Task<GetStaffEditResponse> Handle(GetStaffEditQuery request, CancellationToken cancellationToken) =>
        operations.GetStaffEditAsync(request, cancellationToken);
}
