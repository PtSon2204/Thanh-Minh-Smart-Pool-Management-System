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
    }
}
