using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;

namespace SmartPool.UnitTests.Features.Authentication;

public sealed class TemporaryCredentialSecurityTests
{
    [Fact]
    public void RestrictedToken_HasPurposeAndFingerprint_AndCannotBecomeNormalToken()
    {
        using var database = new SmartPoolDbContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = new string('x', 64),
            ["JwtSettings:Issuer"] = "test", ["JwtSettings:Audience"] = "test",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "60"
        }).Build();
        var service = new AccessTokenService(configuration, database, Mock.Of<IStaffContractAccessPolicy>());
        var user = new User { Id = Guid.NewGuid(), PasswordHash = "synthetic-hash",
            MustChangePassword = true, TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddMinutes(3) };
        var result = service.CreatePasswordChangeToken(user, "SALES");
        var jwt = new JsonWebToken(result.Token);
        Assert.Equal("password_change", jwt.GetClaim("purpose").Value);
        Assert.Equal(64, jwt.GetClaim(AccessTokenService.PasswordVersionClaim).Value.Length);
        Assert.DoesNotContain(user.PasswordHash, result.Token);
        Assert.True(result.ExpiresAtUtc <= user.TemporaryPasswordExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => service.CreateToken(user, "SALES"));
        user.TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Assert.Throws<InvalidOperationException>(() => service.CreatePasswordChangeToken(user, "SALES"));
    }

    [Fact]
    public async Task ReservedPersonalDomain_IsRejectedAtSharedWriteBoundary()
    {
        using var database = new SmartPoolDbContext(new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=synthetic;Password=synthetic").Options);
        database.Users.Add(new User { Id = Guid.NewGuid(), Email = "someone@SMARTPOOL.COM" });
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => database.SaveChangesAsync());
    }
}
