using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;

public sealed class ChangeRolePermissionCommand : IRequest<IActionResult>
{
    public Guid Id { get; set; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public bool? Enabled { get; init; }
}
