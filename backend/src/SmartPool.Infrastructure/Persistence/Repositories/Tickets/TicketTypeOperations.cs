using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Tickets
{
    public class TicketTypeOperations : ITicketTypeOperations
    {
        private readonly SmartPoolDbContext _context;

        public TicketTypeOperations(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<(List<TicketType> Items, int TotalCount)> GetPagedTicketTypesAsync(
            string? searchTerm,
            string? category,
            bool? isActive,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.TicketTypes.AsNoTracking()
                .Where(t => t.IsDeleted != true &&
                           (t.TicketCategory == TicketCategoryEnum.VE_THANG.ToString() ||
                            t.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString()));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var search = searchTerm.ToLower();
                query = query.Where(t => t.Name.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(t => t.TicketCategory == category);
            }

            if (isActive.HasValue)
            {
                if (isActive.Value)
                    query = query.Where(t => t.IsActive == true || t.IsActive == null);
                else
                    query = query.Where(t => t.IsActive == false);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<TicketType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.TicketTypes
                .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true, cancellationToken);
        }

        public async Task<List<TicketType>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.TicketTypes.AsNoTracking()
                .Where(t => t.IsDeleted != true)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TicketType>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids.ToList();
            return await _context.TicketTypes.AsNoTracking()
                .Where(t => idList.Contains(t.Id) && t.IsDeleted != true)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(TicketType ticketType, CancellationToken cancellationToken = default)
        {
            await _context.TicketTypes.AddAsync(ticketType, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(TicketType ticketType, CancellationToken cancellationToken = default)
        {
            _context.TicketTypes.Update(ticketType);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.TicketTypes
                .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {id}");

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ToggleLockAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.TicketTypes
                .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {id}");

            entity.IsActive = !(entity.IsActive ?? true);
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return entity.IsActive.Value;
        }
    }
}
