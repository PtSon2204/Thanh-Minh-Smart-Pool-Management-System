using SmartPool.Application.Common.Models;
using SmartPool.Domain.Entities;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface ITicketTypeOperations
    {
        Task<(List<TicketType> Items, int TotalCount)> GetPagedTicketTypesAsync(
            string? searchTerm, 
            string? category, 
            bool? isActive, 
            int pageIndex, 
            int pageSize, 
            CancellationToken cancellationToken = default);

        Task<TicketType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<TicketType>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<TicketType>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
        Task AddAsync(TicketType ticketType, CancellationToken cancellationToken = default);
        Task UpdateAsync(TicketType ticketType, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ToggleLockAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
