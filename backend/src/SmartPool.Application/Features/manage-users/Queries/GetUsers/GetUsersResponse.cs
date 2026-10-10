namespace SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

public sealed class GetUsersResponse
{
    public Guid Id { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? FullName { get; init; }
    public string? AvatarUrl { get; init; }
    public Guid? RoleId { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
    public DateTime? CreatedAt { get; init; }
}
