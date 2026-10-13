using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;
using SmartPool.Application.Features.ManageRoles.Queries.GetPermissionCatalog;
using SmartPool.Application.Features.ManageRoles.Queries.GetRolePermissions;
using SmartPool.Application.Features.ManageRoles.Queries.GetRoles;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [HttpGet("permissions/catalog")]
    public Task<IActionResult> GetPermissionCatalog(CancellationToken cancellationToken) =>
        sender.Send(new GetPermissionCatalogQuery(), cancellationToken);

    [HttpGet]
    public Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        sender.Send(new GetRolesQuery(), cancellationToken);

    [HttpGet("{id:guid}/permissions")]
    public Task<IActionResult> GetPermissions(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetRolePermissionsQuery(id), cancellationToken);

    [HttpPatch("{id:guid}/permissions")]
    public Task<IActionResult> ChangePermission(
        Guid id, [FromBody] ChangeRolePermissionCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return sender.Send(command, cancellationToken);
    }
}
