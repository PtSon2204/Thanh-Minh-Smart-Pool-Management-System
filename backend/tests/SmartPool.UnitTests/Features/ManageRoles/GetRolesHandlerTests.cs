using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageRoles;
using SmartPool.Application.Features.ManageRoles.Queries.GetRoles;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageRoles;

public sealed class GetRolesHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Có role → 200, danh sách gồm ID, tên và quyền")]
    public async Task Handle_RolesExist_ReturnsRoles()
    {
        var roles = new List<Role>
        {
            new() { Id = Guid.NewGuid(), Name = "CUSTOMER", Permissions = "{\"profiles\":[\"read\"]}" },
            new() { Id = Guid.NewGuid(), Name = "COACH", Permissions = "{\"swim_classes\":[\"create\"]}" }
        };
        var repository = new Mock<IRolePermissionOperations>();
        repository.Setup(x => x.GetRolesAsync(CancellationToken.None)).ReturnsAsync(roles);

        var result = await new GetRolesHandler(repository.Object).Handle(new GetRolesQuery(), CancellationToken.None);

        var response = Assert.IsType<List<RolePermissionResponse>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(roles.Select(x => x.Id), response.Select(x => x.Id));
        Assert.Equal("CUSTOMER", response[0].Name);
        Assert.Equal("read", response[0].Permissions!["profiles"]![0]!.GetValue<string>());
        Assert.Equal("COACH", response[1].Name);
        Assert.Equal("create", response[1].Permissions!["swim_classes"]![0]!.GetValue<string>());
        repository.Verify(x => x.GetRolesAsync(CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - Không có role → 200, danh sách rỗng")]
    public async Task Handle_NoRoles_ReturnsEmptyList()
    {
        var repository = new Mock<IRolePermissionOperations>();
        repository.Setup(x => x.GetRolesAsync(CancellationToken.None)).ReturnsAsync(Array.Empty<Role>());

        var result = await new GetRolesHandler(repository.Object).Handle(new GetRolesQuery(), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<RolePermissionResponse>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact(DisplayName = "UTCID03 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var repository = new Mock<IRolePermissionOperations>();
        var failure = new InvalidOperationException("DB unavailable");
        repository.Setup(x => x.GetRolesAsync(CancellationToken.None)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetRolesHandler(repository.Object).Handle(new GetRolesQuery(), CancellationToken.None)));
    }
}
