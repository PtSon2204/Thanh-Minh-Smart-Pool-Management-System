using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class SafetyAlertsAuthorizationTests
{
    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("Admin", true)]
    [InlineData("admin", true)]
    [InlineData("LIFEGUARD", true)]
    [InlineData("Lifeguard", true)]
    [InlineData("lifeguard", true)]
    [InlineData("CUSTOMER", false)]
    [InlineData("SALES", false)]
    [InlineData("COACH", false)]
    public async Task SafetyEndpoints_ApplyTheSameCaseInsensitiveOperatorBoundary(string role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)
        }, "test"));
        foreach (var action in new[] { nameof(SafetyAlertsController.ActivateAlert), nameof(SafetyAlertsController.GetIncidents),
            nameof(SafetyAlertsController.GetIncidentById) })
        {
            var metadata = typeof(SafetyAlertsController).GetCustomAttributes<AuthorizeAttribute>()
                .Concat(typeof(SafetyAlertsController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.NotNull(policy);
            var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy);
            Assert.Equal(expected, result.Succeeded);
        }
    }
}
