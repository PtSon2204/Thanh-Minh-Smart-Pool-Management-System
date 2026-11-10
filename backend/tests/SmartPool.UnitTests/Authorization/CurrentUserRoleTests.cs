using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartPool.Infrastructure.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class CurrentUserRoleTests
{
    [Theory]
    [InlineData("ADMIN")]
    [InlineData("Admin")]
    [InlineData("admin")]
    public void AdminCaseMatchesApiPolicy(string role)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")) };
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = context });
        Assert.True(user.IsInRole("ADMIN"));
        Assert.False(user.IsInRole("LIFEGUARD"));
    }

    [Fact]
    public void DoesNotGrantRolesToAnonymousPrincipal()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "ADMIN")])) };
        Assert.False(new CurrentUser(new HttpContextAccessor { HttpContext = context }).IsInRole("ADMIN"));
    }
}
