using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class UsersReadAuthorizationTests
{
    [Fact(DisplayName = "UTCID01 (A) - Hai endpoint xem user kế thừa AdminOnly; khách chưa đăng nhập bị từ chối")]
    public async Task ReadEndpoints_RequireAuthenticatedAdmin()
    {
        var attributes = typeof(UsersController).GetCustomAttributes<AuthorizeAttribute>().ToArray();
        Assert.Contains(attributes, x => x.Policy == AuthorizationPolicies.AdminOnly);
        Assert.Empty(typeof(UsersController).GetCustomAttributes<AllowAnonymousAttribute>());
        foreach (var methodName in new[] { nameof(UsersController.GetUsers), nameof(UsersController.GetUserById) })
        {
            var method = typeof(UsersController).GetMethod(methodName)!;
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        }

        using var provider = Services();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False((await authorization.AuthorizeAsync(principal, null, AuthorizationPolicies.AdminOnly)).Succeeded);
    }

    [Fact(DisplayName = "UTCID02 (A) - User CUSTOMER đã đăng nhập bị từ chối xem danh sách và chi tiết")]
    public async Task ReadEndpoints_NonAdminIsDenied()
    {
        using var provider = Services();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        Assert.False((await authorization.AuthorizeAsync(Principal("CUSTOMER"), null, AuthorizationPolicies.AdminOnly)).Succeeded);
    }

    [Fact(DisplayName = "UTCID03 (N) - User ADMIN đã đăng nhập được phép truy cập")]
    public async Task ReadEndpoints_AdminIsAllowed()
    {
        using var provider = Services();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(Principal("ADMIN"), null, AuthorizationPolicies.AdminOnly)).Succeeded);
    }

    private static ClaimsPrincipal Principal(string role)
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        return services.BuildServiceProvider();
    }
}
