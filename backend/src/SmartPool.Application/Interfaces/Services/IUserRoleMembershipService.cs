namespace SmartPool.Application.Interfaces.Services;

public interface IUserRoleMembershipService
{
    Task<IReadOnlyList<string>> GetActiveRolesAsync(Guid userId, string primaryRole,
        CancellationToken cancellationToken = default);
    Task GrantCoachAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeCoachAsync(Guid userId, CancellationToken cancellationToken = default);
}
