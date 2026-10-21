namespace SmartPool.Application.Features.ManageUsers.Commands.CreateUser;

public sealed class CreateUserResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Address { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
