namespace SmartPool.Application.Features.Authentication.Models;

public sealed record RefreshRotationResult(
    Guid Id, string Username, string Role,
    string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc)
{
    public IReadOnlyList<string> Roles { get; init; } = [];
}
