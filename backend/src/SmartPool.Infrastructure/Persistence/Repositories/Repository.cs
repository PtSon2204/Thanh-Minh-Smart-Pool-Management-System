using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using System.Linq.Expressions;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Generic repository implementation dùng EF Core + SmartPoolDbContext.
    /// Không cần tạo Repository riêng cho từng entity trừ khi cần query phức tạp.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly SmartPoolDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(SmartPoolDbContext context)
        {
            _context = context;
            _dbSet   = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _dbSet.FindAsync([id], cancellationToken);

        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => await _dbSet.AnyAsync(predicate, cancellationToken);

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => predicate is null
                ? await _dbSet.CountAsync(cancellationToken)
                : await _dbSet.CountAsync(predicate, cancellationToken);

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await _dbSet.AddAsync(entity, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            => await _dbSet.AddRangeAsync(entities, cancellationToken);

        public void Update(T entity)
            => _dbSet.Update(entity);

        public void Remove(T entity)
            => _dbSet.Remove(entity);

        public void RemoveRange(IEnumerable<T> entities)
            => _dbSet.RemoveRange(entities);
    }
}
