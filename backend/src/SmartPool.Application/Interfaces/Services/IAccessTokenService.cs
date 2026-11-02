using SmartPool.Application.Features.Authentication.Models;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Services;

public interface IAccessTokenService
{
    AccessTokenResult CreateToken(User user, string role);
    AccessTokenResult CreateToken(User user, string role, IReadOnlyCollection<string> roles);
    Task<bool> IsCurrentAsync(Guid userId, string tokenRole, string tokenPasswordVersion,
        CancellationToken cancellationToken);
    Task<bool> IsCurrentRolesAsync(Guid userId, IReadOnlyCollection<string> tokenRoles,
        string tokenPasswordVersion, CancellationToken cancellationToken);
}
