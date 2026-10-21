using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetRolePermissions;

public sealed record GetRolePermissionsQuery(Guid Id) : IRequest<IActionResult>;
