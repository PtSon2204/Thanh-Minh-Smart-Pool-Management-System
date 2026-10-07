using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Authorization;

public sealed class ApiAuthorizationTests
{
    [Theory]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "admin", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "STAFF", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "staff", AuthorizationPolicies.AdminOrStaff, true)]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "not-a-guid", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "CUSTOMER", AuthorizationPolicies.AdminOrStaff, false)]
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
    [InlineData("Staff", RoleEnum.STAFF, true)]
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
    public void ProtectedControllers_ApplyBaselinePolicyAndAdminOverridesToAll30Actions()
    {
        var expectedAdminOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(ServicesController.CreateService),
            nameof(ServicesController.UpdateService),
            nameof(ServicesController.SetServiceStatus),
            nameof(StaffsController.GetStaffs),
            nameof(StaffsController.CreateStaff),
            nameof(StaffsController.GetOptions),
            nameof(StaffsController.UpdateStaff),
            nameof(StaffsController.GetSchedule),
            nameof(StaffsController.AssignShift),
            nameof(StaffsController.CancelShift),
            nameof(StaffsController.GetSalaries),
            nameof(StaffsController.CreateSalary),
            nameof(StaffsController.UpdateSalary),
            nameof(ShiftsController.CreateShift),
            nameof(ShiftsController.UpdateShift)
        };
        var controllers = new[]
        {
            typeof(ServicesController), typeof(StaffsController), typeof(ShiftsController),
            typeof(RentalsController), typeof(PoolAccessController)
        };
        var actions = controllers.SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => (Controller: controller, Method: method)))
            .ToArray();

        Assert.Equal(31, actions.Length);
        foreach (var controller in controllers)
        {
            Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(), attribute =>
                attribute.Policy == AuthorizationPolicies.AdminOrStaff);
        }

        foreach (var action in actions)
        {
            var isAdminOnly = expectedAdminOnly.Contains(action.Method.Name);
            Assert.Equal(isAdminOnly, action.Method.GetCustomAttributes<AuthorizeAttribute>()
                .Any(attribute => attribute.Policy == AuthorizationPolicies.AdminOnly));
        }
    }

    private static ClaimsPrincipal CreatePrincipal(bool isAuthenticated, string userId, string role)
    {
        var identity = new ClaimsIdentity(isAuthenticated ? "test" : null);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
