using Microsoft.AspNetCore.Authorization;
using SmartPool.Application.Common.Authorization;

namespace SmartPool.API.Authorization;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string module, string action)
    {
        if (!PermissionCatalog.IsValid(module, action))
            throw new ArgumentException($"Unknown permission: {module}.{action}");
        Policy = PermissionCatalog.Policy(module, action);
    }
}
