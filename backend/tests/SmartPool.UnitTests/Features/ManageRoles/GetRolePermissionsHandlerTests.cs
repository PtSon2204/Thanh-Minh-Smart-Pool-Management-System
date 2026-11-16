using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageRoles;
using SmartPool.Application.Features.ManageRoles.Queries.GetRolePermissions;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageRoles;

public sealed class GetRolePermissionsHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Role tồn tại → 200, đúng ID, tên và quyền")]
    public async Task Handle_RoleExists_ReturnsPermissions()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "SALES", Permissions = "{\"ticket_sales\":[\"create\"]}" };
        var repository = Repository(role);

        var result = await new GetRolePermissionsHandler(repository.Object)
            .Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(role.Id, response.Id);
        Assert.Equal("SALES", response.Name);
        Assert.Equal("create", response.Permissions!["ticket_sales"]![0]!.GetValue<string>());
        repository.Verify(x => x.GetRoleAsync(role.Id, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Role không tồn tại hoặc đã xóa → 404")]
    public async Task Handle_RoleUnavailable_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var repository = Repository(null);

        Assert.IsType<NotFoundResult>(await new GetRolePermissionsHandler(repository.Object)
            .Handle(new GetRolePermissionsQuery(id), CancellationToken.None));
        repository.Verify(x => x.GetRoleAsync(id, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (B) - Quyền rỗng → 200, permissions là object rỗng")]
    public async Task Handle_EmptyPermissions_ReturnsEmptyObject()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "COACH", Permissions = " " };
        var repository = Repository(role);

        var result = await new GetRolePermissionsHandler(repository.Object)
            .Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Empty(Assert.IsType<System.Text.Json.Nodes.JsonObject>(response.Permissions));
    }

    [Fact(DisplayName = "UTCID04 (A) - JSON quyền lỗi → 200, permissions là null")]
    public async Task Handle_InvalidPermissionsJson_ReturnsNullPermissions()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "COACH", Permissions = "not-json" };
        var repository = Repository(role);

        var result = await new GetRolePermissionsHandler(repository.Object)
            .Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Null(response.Permissions);
    }

    private static Mock<IRolePermissionOperations> Repository(Role? role)
    {
        var repository = new Mock<IRolePermissionOperations>();
        repository.Setup(x => x.GetRoleAsync(It.IsAny<Guid>(), CancellationToken.None)).ReturnsAsync(role);
        return repository;
    }
}
