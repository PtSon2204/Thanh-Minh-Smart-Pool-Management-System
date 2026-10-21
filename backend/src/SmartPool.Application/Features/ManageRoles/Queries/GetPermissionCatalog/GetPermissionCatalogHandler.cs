using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Authorization;

namespace SmartPool.Application.Features.ManageRoles.Queries.GetPermissionCatalog;

public sealed class GetPermissionCatalogHandler : IRequestHandler<GetPermissionCatalogQuery, IActionResult>
{
    public Task<IActionResult> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(new OkObjectResult(PermissionCatalog.All));
}
