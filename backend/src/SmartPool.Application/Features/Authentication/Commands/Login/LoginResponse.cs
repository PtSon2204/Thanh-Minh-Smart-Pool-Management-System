namespace SmartPool.Application.Features.Authentication.Commands.Login;

public sealed class LoginResponse
{
    public Guid Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }
}
