namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}
