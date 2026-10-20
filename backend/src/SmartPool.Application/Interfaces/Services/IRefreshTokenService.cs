namespace SmartPool.Application.Interfaces.Services;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult?> CreateAsync(Guid userId, string expectedPasswordHash,
        CancellationToken cancellationToken = default);

    Task<RefreshRotationResult?> RotateAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAsync(string token, CancellationToken cancellationToken = default);

    string ComputeHash(string token);
}

public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record RefreshRotationResult(
    Guid Id, string Username, string Role,
    string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
