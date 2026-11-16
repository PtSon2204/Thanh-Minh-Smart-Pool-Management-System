using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Features.ManageRoles.Queries.GetPermissionCatalog;

namespace SmartPool.UnitTests.Features.ManageRoles;

public sealed class GetPermissionCatalogHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Xem catalog quyền → 200, các module/action được hỗ trợ")]
    public async Task Handle_ReturnsSupportedPermissions()
    {
        var result = await new GetPermissionCatalogHandler()
            .Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        var catalog = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Same(PermissionCatalog.All, catalog);
        Assert.Contains("read", catalog["profiles"]);
        Assert.Contains("update", catalog["profiles"]);
        Assert.Contains("create", catalog["ticket_sales"]);
        Assert.False(catalog.ContainsKey("unknown"));
        Assert.DoesNotContain("delete", catalog["profiles"]);
    }
}
