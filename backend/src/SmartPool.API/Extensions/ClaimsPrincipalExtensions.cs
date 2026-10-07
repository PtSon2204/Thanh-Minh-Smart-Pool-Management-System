using System.Security.Claims;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool HasRole(this ClaimsPrincipal principal, RoleEnum role)
    {
        return principal.FindAll(ClaimTypes.Role)
            .Any(claim => string.Equals(claim.Value, role.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && userId != Guid.Empty;
    }
}
