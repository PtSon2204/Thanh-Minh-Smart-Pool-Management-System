using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.SafetyMonitoring
{
    public class SafetyAlertOperations : ISafetyAlertOperations
    {
        private readonly SmartPoolDbContext _context;

        public SafetyAlertOperations(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<Incident?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        public async Task<List<Incident>> GetAllIncidentsAsync(CancellationToken cancellationToken = default)
            => await _context.Incidents.AsNoTracking().ToListAsync(cancellationToken);

        public async Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
        {
            await _context.Incidents.AddAsync(incident, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
        {
            _context.Incidents.Update(incident);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        public async Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default)
            => await _context.Users.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<UserProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        public async Task<List<UserProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
            => await _context.UserProfiles.AsNoTracking().ToListAsync(cancellationToken);
    }
}
