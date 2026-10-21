using SmartPool.Application.Features.Authentication.Models;
namespace SmartPool.Application.Interfaces.Services;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult?> CreateAsync(Guid userId, string expectedPasswordHash,
        CancellationToken cancellationToken = default);

    Task<RefreshRotationResult?> RotateAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAsync(string token, CancellationToken cancellationToken = default);

    string ComputeHash(string token);
}
