using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface ISafetyAlertOperations
    {
        Task<Incident?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken = default);
        Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UserProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
