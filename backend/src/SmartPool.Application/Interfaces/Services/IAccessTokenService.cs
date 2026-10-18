using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Services;

public interface IAccessTokenService
{
    AccessTokenResult CreateToken(User user, string role);
    Task<bool> IsCurrentAsync(Guid userId, string tokenRole, string tokenPasswordVersion,
        CancellationToken cancellationToken);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
