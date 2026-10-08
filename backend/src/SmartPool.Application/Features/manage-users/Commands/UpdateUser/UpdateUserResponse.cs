namespace SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

public sealed class UpdateUserResponse
{
    public Guid Id { get; init; }
    public string? Username { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Address { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
