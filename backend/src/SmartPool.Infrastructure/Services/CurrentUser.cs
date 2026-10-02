using Microsoft.AspNetCore.Http;
using SmartPool.Application.Interfaces.Services;
using System.Security.Claims;

namespace SmartPool.Infrastructure.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? AuthenticatedPrincipal
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            return principal?.Identity?.IsAuthenticated == true ? principal : null;
        }
    }

    public bool IsAuthenticated => AuthenticatedPrincipal is not null;

    public Guid? UserId
    {
        get
        {
            var principal = AuthenticatedPrincipal;
            if (principal is null)
                return null;

            var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            return Guid.TryParse(rawId, out var userId) && userId != Guid.Empty
                ? userId
                : null;
        }
    }

    public string? Role
    {
        get
        {
            var principal = AuthenticatedPrincipal;
            return principal?.FindFirstValue(ClaimTypes.Role)
                ?? principal?.FindFirstValue("role");
        }
    }

    public bool IsInRole(string role)
    {
        var principal = AuthenticatedPrincipal;
        return principal is not null && !string.IsNullOrWhiteSpace(role) &&
            (principal.IsInRole(role) || string.Equals(Role, role, StringComparison.Ordinal));
    }
}
