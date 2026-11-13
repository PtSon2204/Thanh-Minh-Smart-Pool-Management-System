using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    private const string Policy = "permission:profiles:read";

    [Fact(DisplayName = "UTCID01 (N) - User có quyền hiện tại → cho phép")]
    public async Task Authorize_CurrentPermission_AllowsAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"profiles\":[\"read\"]}");

        Assert.True(await h.Authorize(Principal(h.UserId, "CUSTOMER")));
        h.Reader.Verify(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - User không có quyền → từ chối")]
    public async Task Authorize_PermissionMissing_DeniesAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"profiles\":[\"update\"]}");

        Assert.False(await h.Authorize(Principal(h.UserId, "CUSTOMER")));
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu hoặc sai user ID → từ chối, không đọc quyền")]
    public async Task Authorize_InvalidUserId_DeniesBeforeReadingPermissions()
    {
        var h = new Harness();

        Assert.False(await h.Authorize(Principal((string?)null, "CUSTOMER")));
        Assert.False(await h.Authorize(Principal("not-a-guid", "CUSTOMER")));
        Assert.False(await h.Authorize(Principal(Guid.Empty.ToString(), "CUSTOMER")));
        h.Reader.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Thiếu role claim → từ chối, không đọc quyền")]
    public async Task Authorize_NoRoleClaim_DeniesBeforeReadingPermissions()
    {
        var h = new Harness();

        Assert.False(await h.Authorize(Principal(h.UserId)));
        h.Reader.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Reader không trả quyền → từ chối")]
    public async Task Authorize_NoCurrentPermissions_DeniesAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        Assert.False(await h.Authorize(Principal(h.UserId, "CUSTOMER")));
    }

    [Fact(DisplayName = "UTCID06 (A) - JSON quyền hiện tại lỗi → từ chối")]
    public async Task Authorize_MalformedPermissions_DeniesAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-json");

        Assert.False(await h.Authorize(Principal(h.UserId, "CUSTOMER")));
    }

    [Fact(DisplayName = "UTCID07 (A) - Action hoặc dạng module không khớp → từ chối")]
    public async Task Authorize_InvalidModuleShape_DeniesAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"profiles\":\"read\"}");

        Assert.False(await h.Authorize(Principal(h.UserId, "CUSTOMER")));
    }

    [Fact(DisplayName = "UTCID08 (B) - Nhiều role, một role có quyền → cho phép")]
    public async Task Authorize_SecondRoleHasPermission_AllowsAccess()
    {
        var h = new Harness();
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "COACH", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"profiles\":[\"read\"]}");

        Assert.True(await h.Authorize(Principal(h.UserId, "CUSTOMER", "COACH")));
        h.Reader.Verify(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()), Times.Once);
        h.Reader.Verify(x => x.ReadCurrentPermissionsAsync(h.UserId, "COACH", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (B) - Cấp rồi thu hồi quyền → request sau dùng dữ liệu mới")]
    public async Task Authorize_PermissionChanges_AppliesOnNextRequest()
    {
        var h = new Harness();
        var permissions = "{}";
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => permissions);
        var user = Principal(h.UserId, "CUSTOMER");

        Assert.False(await h.Authorize(user));
        permissions = "{\"profiles\":[\"read\"]}";
        Assert.True(await h.Authorize(user));
        permissions = "{}";
        Assert.False(await h.Authorize(user));
        h.Reader.Verify(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "UTCID10 (A) - Reader ném exception → truyền tiếp exception")]
    public async Task Authorize_ReaderThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Reader.Setup(x => x.ReadCurrentPermissionsAsync(h.UserId, "CUSTOMER", It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Authorize(Principal(h.UserId, "CUSTOMER"))));
    }

    private static ClaimsPrincipal Principal(Guid? userId, params string[] roles) =>
        Principal(userId?.ToString(), roles);

    private static ClaimsPrincipal Principal(string? userId, params string[] roles)
    {
        var identity = new ClaimsIdentity(authenticationType: "test");
        if (userId is not null) identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        foreach (var role in roles) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private sealed class Harness : IDisposable
    {
        public readonly Guid UserId = Guid.NewGuid();
        public readonly Mock<IRolePermissionReader> Reader = new();
        private readonly ServiceProvider _provider;

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Reader.Object);
            services.AddApiAuthorization();
            _provider = services.BuildServiceProvider();
        }

        public async Task<bool> Authorize(ClaimsPrincipal user)
        {
            using var scope = _provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            return (await service.AuthorizeAsync(user, new DefaultHttpContext(), Policy)).Succeeded;
        }

        public void Dispose() => _provider.Dispose();
    }
}
