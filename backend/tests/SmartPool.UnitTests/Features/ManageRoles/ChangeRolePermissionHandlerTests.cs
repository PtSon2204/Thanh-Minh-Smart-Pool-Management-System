using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageRoles;
using SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageRoles;

public sealed class ChangeRolePermissionHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Bật quyền hợp lệ → 200, lưu quyền mới")]
    public async Task Handle_EnablePermission_UpdatesRole()
    {
        var h = new Harness();
        h.Role.Permissions = "{\"profiles\":[\"read\"]}";

        var result = await h.Handle("profiles", "update", true);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(h.Role.Id, response.Id);
        Assert.Equal("CUSTOMER", response.Name);
        Assert.Equal(new[] { "read", "update" }, Actions(response, "profiles"));
        h.AssertUpdatedOnce();
    }

    [Fact(DisplayName = "UTCID02 (N) - Tắt quyền cuối cùng → 200, xóa module rỗng")]
    public async Task Handle_DisableLastPermission_RemovesModule()
    {
        var h = new Harness();
        h.Role.Permissions = "{\"profiles\":[\"read\"],\"reports\":[\"read\"]}";

        var result = await h.Handle("profiles", "read", false);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Null(response.Permissions!["profiles"]);
        Assert.Equal(new[] { "read" }, Actions(response, "reports"));
        h.AssertUpdatedOnce();
    }

    [Fact(DisplayName = "UTCID03 (B) - Bật quyền đã có hoặc tắt quyền chưa có → không tạo quyền trùng")]
    public async Task Handle_RepeatedChange_IsIdempotent()
    {
        var h = new Harness();
        h.Role.Permissions = "{\"profiles\":[\"read\"]}";

        var first = await h.Handle("profiles", "read", true);
        var second = await h.Handle("profiles", "update", false);

        Assert.Equal(new[] { "read" }, Actions(Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(first).Value), "profiles"));
        Assert.Equal(new[] { "read" }, Actions(Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(second).Value), "profiles"));
        h.Users.Verify(x => x.UpdatePermissionsAsync(h.Role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "UTCID04 (B) - Module/action có chữ hoa và khoảng trắng → chuẩn hóa")]
    public async Task Handle_UppercaseAndWhitespace_NormalizesInput()
    {
        var h = new Harness();

        var result = await h.Handle(" PROFILES ", " UPDATE ", true);

        var response = Assert.IsType<RolePermissionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new[] { "update" }, Actions(response, "profiles"));
        h.AssertUpdatedOnce();
    }

    [Fact(DisplayName = "UTCID05 (A) - Module không hợp lệ → 400, không cập nhật")]
    public async Task Handle_InvalidModule_ReturnsBadRequest()
    {
        var h = new Harness();
        AssertBadRequest(await h.Handle("unknown", "read", true));
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Action không hợp lệ → 400, không cập nhật")]
    public async Task Handle_InvalidAction_ReturnsBadRequest()
    {
        var h = new Harness();
        AssertBadRequest(await h.Handle("profiles", "delete", true));
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Enabled thiếu → 400, không cập nhật")]
    public async Task Handle_MissingEnabled_ReturnsBadRequest()
    {
        var h = new Harness();
        AssertBadRequest(await h.Handle("profiles", "read", null));
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (A) - Role không tồn tại hoặc đã xóa → 404")]
    public async Task Handle_RoleUnavailable_ReturnsNotFound()
    {
        var h = new Harness();
        h.Users.Setup(x => x.UpdatePermissionsAsync(h.Role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        Assert.IsType<NotFoundResult>(await h.Handle("profiles", "read", true));
        h.AssertUpdatedOnce();
    }

    [Fact(DisplayName = "UTCID09 (A) - Role ADMIN không được chỉnh quyền → 409")]
    public async Task Handle_UnsupportedRole_ReturnsConflict()
    {
        var h = new Harness();
        h.Role.Name = "ADMIN";

        AssertConflict(await h.Handle("profiles", "read", true),
            "Chỉ có thể chỉnh quyền của LIFEGUARD, SALES, COACH hoặc CUSTOMER.");
        Assert.Null(h.Role.Permissions);
    }

    [Fact(DisplayName = "UTCID10 (A) - JSON quyền hiện tại không hợp lệ → 409")]
    public async Task Handle_InvalidPermissionsJson_ReturnsConflict()
    {
        var h = new Harness();
        h.Role.Permissions = "not-json";

        AssertConflict(await h.Handle("profiles", "read", true), "Dữ liệu quyền hiện tại không hợp lệ.");
        Assert.Equal("not-json", h.Role.Permissions);
    }

    [Fact(DisplayName = "UTCID11 (A) - Quyền của module sai dạng → 409")]
    public async Task Handle_InvalidModulePermissions_ReturnsConflict()
    {
        var h = new Harness();
        h.Role.Permissions = "{\"profiles\":true}";

        AssertConflict(await h.Handle("profiles", "read", true), "Dữ liệu quyền của module không hợp lệ.");
        Assert.Equal("{\"profiles\":true}", h.Role.Permissions);
    }

    [Fact(DisplayName = "UTCID12 (A) - Repository ném exception → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.UpdatePermissionsAsync(h.Role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle("profiles", "read", true)));
        h.AssertUpdatedOnce();
    }

    private static string[] Actions(RolePermissionResponse response, string module) =>
        Assert.IsType<JsonArray>(response.Permissions![module]).Select(value => value!.GetValue<string>()).ToArray();

    private static void AssertBadRequest(IActionResult result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Module, action hoặc enabled không hợp lệ.", Message(badRequest.Value));
    }

    private static void AssertConflict(IActionResult result, string message)
    {
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(message, Message(conflict.Value));
    }

    private static string? Message(object? value) =>
        System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(value))
            .RootElement.GetProperty("message").GetString();

    private sealed class Harness
    {
        public readonly Mock<IRolePermissionOperations> Users = new();
        public readonly Role Role = new() { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        private readonly ChangeRolePermissionHandler _handler;

        public Harness()
        {
            Users.Setup(x => x.UpdatePermissionsAsync(Role.Id, It.IsAny<Func<Role, string>>(), It.IsAny<CancellationToken>()))
                .Returns((Guid _, Func<Role, string> change, CancellationToken _) =>
                {
                    Role.Permissions = change(Role);
                    return Task.FromResult<Role?>(Role);
                });
            _handler = new ChangeRolePermissionHandler(Users.Object);
        }

        public Task<IActionResult> Handle(string? module, string? action, bool? enabled) =>
            _handler.Handle(new ChangeRolePermissionCommand { Id = Role.Id, Module = module, Action = action, Enabled = enabled }, CancellationToken.None);

        public void AssertUpdatedOnce() => Users.Verify(x => x.UpdatePermissionsAsync(
            Role.Id, It.IsAny<Func<Role, string>>(), CancellationToken.None), Times.Once);
    }
}
