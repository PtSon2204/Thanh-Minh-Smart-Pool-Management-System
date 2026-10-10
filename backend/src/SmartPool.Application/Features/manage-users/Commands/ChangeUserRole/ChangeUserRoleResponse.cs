namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;

public sealed class ChangeUserRoleResponse
{
    public Guid Id { get; init; }
    public Guid RoleId { get; init; }
    public string Role { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}
