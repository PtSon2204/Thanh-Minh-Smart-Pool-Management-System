using System.Text.Json;
using System.Text.Json.Nodes;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageRoles;

public sealed class RolePermissionResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public JsonNode? Permissions { get; init; }

    public static RolePermissionResponse FromRole(Role role)
    {
        JsonNode? permissions;
        try
        {
            permissions = string.IsNullOrWhiteSpace(role.Permissions)
                ? new JsonObject()
                : JsonNode.Parse(role.Permissions);
        }
        catch (JsonException)
        {
            permissions = null;
        }

        return new RolePermissionResponse
        {
            Id = role.Id,
            Name = role.Name,
            Permissions = permissions
        };
    }
}
