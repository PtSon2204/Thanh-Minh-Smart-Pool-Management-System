using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageRoles;
using SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;
using SmartPool.Application.Features.ManageRoles.Queries.GetRolePermissions;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Authorization;

public sealed class RolePermissionHandlerTests
{
    [Fact]
    public async Task ChangePermission_GrantsAndRevokesWithoutLosingOtherPermissions()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(), Name = "LIFEGUARD",
            Permissions = """{"profiles":["read"],"ticket_lookup":[]}"""
        };
        var operations = new Mock<IRolePermissionOperations>();
        operations.Setup(item => item.UpdatePermissionsAsync(role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Func<Role, string> change, CancellationToken _) =>
            {
                role.Permissions = change(role);
                return role;
            });
        var handler = new ChangeRolePermissionHandler(operations.Object);

        var granted = Assert.IsType<OkObjectResult>(await handler.Handle(new ChangeRolePermissionCommand
        {
            Id = role.Id, Module = " Ticket_Lookup ", Action = " READ ", Enabled = true
        }, CancellationToken.None));
        Assert.Equal("read", Assert.IsType<RolePermissionResponse>(granted.Value).Permissions!["ticket_lookup"]![0]!.GetValue<string>());
        Assert.Contains("\"profiles\":[\"read\"]", role.Permissions);

        await handler.Handle(new ChangeRolePermissionCommand
        {
            Id = role.Id, Module = "ticket_lookup", Action = "read", Enabled = false
        }, CancellationToken.None);
        Assert.DoesNotContain("ticket_lookup", role.Permissions);
        Assert.Contains("\"profiles\":[\"read\"]", role.Permissions);
    }

    [Theory]
    [InlineData("ADMIN", "{}")]
    [InlineData("LIFEGUARD", "{")]
    [InlineData("LIFEGUARD", """{"ticket_lookup":{}}""")]
    public async Task ChangePermission_RejectsProtectedRoleOrMalformedPermissions(string roleName, string permissions)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = roleName, Permissions = permissions };
        var operations = new Mock<IRolePermissionOperations>();
        operations.Setup(item => item.UpdatePermissionsAsync(role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Func<Role, string> change, CancellationToken _) =>
            {
                change(role);
                return role;
            });

        var result = await new ChangeRolePermissionHandler(operations.Object).Handle(new ChangeRolePermissionCommand
        {
            Id = role.Id, Module = "ticket_lookup", Action = "read", Enabled = true
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(permissions, role.Permissions);
    }

    [Fact]
    public async Task ChangePermission_RejectsInvalidRequestBeforeStorage()
    {
        var operations = new Mock<IRolePermissionOperations>();
        var result = await new ChangeRolePermissionHandler(operations.Object).Handle(new ChangeRolePermissionCommand
        {
            Id = Guid.NewGuid(), Module = "unknown", Action = "read", Enabled = true
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsNotFoundWhenRoleIsMissing()
    {
        var operations = new Mock<IRolePermissionOperations>();
        var result = await new GetRolePermissionsHandler(operations.Object)
            .Handle(new GetRolePermissionsQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }
}
