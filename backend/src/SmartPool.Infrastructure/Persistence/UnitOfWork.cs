using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence
{

    public class UnitOfWork : IUnitOfWork
    {
        private readonly SmartPoolDbContext _context;

        public UnitOfWork(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);

        public void Dispose()
            => _context.Dispose();
    }
}
