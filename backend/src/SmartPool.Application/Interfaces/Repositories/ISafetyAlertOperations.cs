using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface ISafetyAlertOperations
    {
        Task<Incident?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<Incident>> GetAllIncidentsAsync(CancellationToken cancellationToken = default);
        Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken = default);
        Task UpdateIncidentAsync(Incident incident, CancellationToken cancellationToken = default);
        Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task<UserProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<List<UserProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
    }
}
