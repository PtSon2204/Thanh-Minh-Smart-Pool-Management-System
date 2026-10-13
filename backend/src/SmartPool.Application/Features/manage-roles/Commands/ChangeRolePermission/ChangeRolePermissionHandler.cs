using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;

public sealed class ChangeRolePermissionHandler(IRolePermissionOperations roles)
    : IRequestHandler<ChangeRolePermissionCommand, IActionResult>
{
    public async Task<IActionResult> Handle(ChangeRolePermissionCommand request, CancellationToken cancellationToken)
    {
        var module = request.Module?.Trim().ToLowerInvariant();
        var action = request.Action?.Trim().ToLowerInvariant();
        if (!PermissionCatalog.IsValid(module, action) || request.Enabled is null)
            return new BadRequestObjectResult(new { message = "Module, action hoặc enabled không hợp lệ." });

        try
        {
            var role = await roles.UpdatePermissionsAsync(request.Id,
                current => ApplyChange(current, module!, action!, request.Enabled.Value), cancellationToken);
            return role is null
                ? new NotFoundResult()
                : new OkObjectResult(RolePermissionResponse.FromRole(role));
        }
        catch (RolePermissionConflictException exception)
        {
            return new ConflictObjectResult(new { message = exception.Message });
        }
    }

    private static string ApplyChange(Role role, string module, string action, bool enabled)
    {
        if (!string.Equals(role.Name, "LIFEGUARD", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role.Name, "SALES", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role.Name, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            throw new RolePermissionConflictException("Chỉ có thể chỉnh quyền của LIFEGUARD, SALES hoặc CUSTOMER.");

        JsonObject permissions;
        try
        {
            permissions = string.IsNullOrWhiteSpace(role.Permissions)
                ? new JsonObject()
                : JsonNode.Parse(role.Permissions) as JsonObject
                    ?? throw new JsonException("Permissions must be a JSON object.");
        }
        catch (JsonException)
        {
            throw new RolePermissionConflictException("Dữ liệu quyền hiện tại không hợp lệ.");
        }

        var current = permissions[module] as JsonArray;
        if (permissions[module] is not null && current is null)
            throw new RolePermissionConflictException("Dữ liệu quyền của module không hợp lệ.");

        var granted = new HashSet<string>(StringComparer.Ordinal);
        if (current is not null)
        {
            try
            {
                foreach (var value in current)
                    if (value is not null) granted.Add(value.GetValue<string>());
            }
            catch (InvalidOperationException)
            {
                throw new RolePermissionConflictException("Dữ liệu quyền của module không hợp lệ.");
            }
        }

        if (enabled) granted.Add(action);
        else granted.Remove(action);

        if (granted.Count == 0) permissions.Remove(module);
        else permissions[module] = new JsonArray(PermissionCatalog.All[module]
            .Where(granted.Contains).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

        return permissions.ToJsonString();
    }
}

public sealed class RolePermissionConflictException(string message) : Exception(message);
