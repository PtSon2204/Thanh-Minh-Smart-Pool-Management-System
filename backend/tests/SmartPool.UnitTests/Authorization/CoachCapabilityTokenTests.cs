using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.Authorization;
using Moq;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class CoachCapabilityTokenTests
{
    [Fact]
    public async Task CustomerAndCoachClaimsUnionCurrentRolePermissions()
    {
        var reader = new Mock<IRolePermissionReader>();
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), "CUSTOMER",
            It.IsAny<CancellationToken>())).ReturnsAsync("{}");
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), "COACH",
            It.IsAny<CancellationToken>())).ReturnsAsync("{\"swim_classes\":[\"read\"]}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader.Object);
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, "CUSTOMER"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "COACH"));

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(identity), null, PermissionCatalog.Policy("swim_classes", "read"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ApprovedCoachTokenContainsCustomerAndCoachWithoutChangingPrimaryRole()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = new string('x', 40),
                ["JwtSettings:Issuer"] = "SmartPool",
                ["JwtSettings:Audience"] = "SmartPoolClients",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "60"
            }).Build();
        var service = new AccessTokenService(configuration, new SmartPoolDbContext());
        var user = new User { Id = Guid.NewGuid() };

        var token = service.CreateToken(user, "CUSTOMER", ["CUSTOMER", "COACH"]);
        var claims = new JsonWebTokenHandler().ReadJsonWebToken(token.Token).Claims;

        Assert.Equal(["CUSTOMER", "COACH"], claims.Where(item => item.Type == ClaimTypes.Role)
            .Select(item => item.Value).ToArray());
    }
}
