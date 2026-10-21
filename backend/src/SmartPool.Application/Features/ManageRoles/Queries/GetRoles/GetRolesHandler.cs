using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetRoles;

public sealed class GetRolesHandler(IRolePermissionOperations roles) : IRequestHandler<GetRolesQuery, IActionResult>
{
    public async Task<IActionResult> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var result = await roles.GetRolesAsync(cancellationToken);
        return new OkObjectResult(result.Select(RolePermissionResponse.FromRole).ToList());
    }
}
