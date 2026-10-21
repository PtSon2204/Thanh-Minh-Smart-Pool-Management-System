using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetRolePermissions;

public sealed class GetRolePermissionsHandler(IRolePermissionOperations roles)
    : IRequestHandler<GetRolePermissionsQuery, IActionResult>
{
    public async Task<IActionResult> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var role = await roles.GetRoleAsync(request.Id, cancellationToken);
        return role is null ? new NotFoundResult() : new OkObjectResult(RolePermissionResponse.FromRole(role));
    }
}
