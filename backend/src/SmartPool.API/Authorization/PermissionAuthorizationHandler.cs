using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.API.Authorization;

public sealed class PermissionAuthorizationHandler(IRolePermissionReader reader)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            userId == Guid.Empty)
            return;

        var tokenRoles = context.User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (tokenRoles.Length == 0) return;

        // Resolve the current account and role on every request so grants and
        // revocations take effect without issuing a new JWT.
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        foreach (var tokenRole in tokenRoles)
        {
            var permissions = await reader.ReadCurrentPermissionsAsync(userId, tokenRole, cancellationToken);
            if (string.IsNullOrWhiteSpace(permissions)) continue;
            try
            {
                using var json = JsonDocument.Parse(permissions);
                if (json.RootElement.ValueKind == JsonValueKind.Object &&
                    json.RootElement.TryGetProperty(requirement.Module, out var actions) &&
                    actions.ValueKind == JsonValueKind.Array &&
                    actions.EnumerateArray().Any(action => action.ValueKind == JsonValueKind.String &&
                        string.Equals(action.GetString(), requirement.Action, StringComparison.Ordinal)))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
            catch (JsonException)
            {
                // Invalid role permissions fail closed for this role.
            }
        }
    }
}
