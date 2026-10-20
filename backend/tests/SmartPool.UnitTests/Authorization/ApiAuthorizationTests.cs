using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Authorization;

public sealed class ApiAuthorizationTests
{
    [Theory]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "admin", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "SALES", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "LIFEGUARD", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "not-a-guid", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "CUSTOMER", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(false, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    public async Task AuthorizeAsync_RequiresAuthenticatedCallerWithValidIdAndPermittedRole(
        bool isAuthenticated,
        string userId,
        string role,
        string policy,
        bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity(isAuthenticated ? "test" : null);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));

        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("ADMIN", RoleEnum.ADMIN, true)]
    [InlineData("admin", RoleEnum.ADMIN, true)]
    [InlineData("Sales", RoleEnum.SALES, true)]
    [InlineData("Lifeguard", RoleEnum.LIFEGUARD, true)]
    [InlineData("CUSTOMER", RoleEnum.ADMIN, false)]
    public void HasRole_UsesCaseInsensitiveRoleClaims(string role, RoleEnum expectedRole, bool expected)
    {
        var principal = CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role);

        Assert.Equal(expected, principal.HasRole(expectedRole));
    }

    [Fact]
    public void TryGetUserId_RejectsEmptyGuid()
    {
        var principal = CreatePrincipal(true, Guid.Empty.ToString(), "ADMIN");

        Assert.False(principal.TryGetUserId(out _));
    }

    [Fact]
    public void HasRole_IgnoresClaimsOutsideTheRoleClaimType()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("role", "ADMIN"));
        var principal = new ClaimsPrincipal(identity);

        Assert.False(principal.HasRole(RoleEnum.ADMIN));
    }

    [Fact]
    public void OperationalEndpoints_RequirePermissions()
    {
        var controllers = new[]
        {
            typeof(ServicesController), typeof(StaffsController), typeof(ShiftsController),
            typeof(RentalsController), typeof(PoolAccessController), typeof(TicketController),
            typeof(NotificationsController), typeof(PaymentsController), typeof(ReportsController),
            typeof(ProfilesController)
        };
        var actions = controllers.SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => (Controller: controller, Method: method)))
            .ToArray();

        foreach (var action in actions)
        {
            var policies = action.Method.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy);
            Assert.Contains(policies, policy => policy?.StartsWith("permission:", StringComparison.Ordinal) == true ||
                policy == AuthorizationPolicies.AdminOnly);
        }
    }

    [Fact]
    public void TicketLookupGrant_DoesNotImplyEntryConfirmationOrHistory()
    {
        Assert.Equal(PermissionCatalog.Policy("ticket_lookup", "read"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.Lookup))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
        Assert.Equal(PermissionCatalog.Policy("pool_entry", "create"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.Confirm))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
        Assert.Equal(PermissionCatalog.Policy("pool_entry", "read"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.GetHistory))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
    }

    [Fact]
    public void PermissionManagement_RequiresAdmin()
    {
        var policy = Assert.Single(typeof(RolesController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.AdminOnly, policy.Policy);
        Assert.False(PermissionCatalog.IsValid("users", "create"));
        Assert.True(PermissionCatalog.IsValid("ticket_lookup", "read"));
    }

    [Theory]
    [InlineData("ADMIN", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("admin", true, true)]
    [InlineData("SALES", true, false)]
    [InlineData("LIFEGUARD", true, false)]
    [InlineData("ADMIN", false, false)]
    public async Task ShiftListing_UsesAdminPolicyAndStillRequiresReadPermission(string role, bool hasPermission, bool expected)
    {
        var reader = new Mock<IRolePermissionReader>();
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission ? "{\"shifts\":[\"read\"]}" : "{}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader.Object);
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(ShiftsController).GetMethod(nameof(ShiftsController.GetShifts))!
            .GetCustomAttributes<AuthorizeAttribute>();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        Assert.NotNull(policy);

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    private static ClaimsPrincipal CreatePrincipal(bool isAuthenticated, string userId, string role)
    {
        var identity = new ClaimsIdentity(isAuthenticated ? "test" : null);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
