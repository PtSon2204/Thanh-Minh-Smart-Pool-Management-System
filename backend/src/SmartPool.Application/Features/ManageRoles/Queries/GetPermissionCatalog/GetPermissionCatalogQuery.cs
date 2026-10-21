using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetPermissionCatalog;

public sealed record GetPermissionCatalogQuery : IRequest<IActionResult>;
