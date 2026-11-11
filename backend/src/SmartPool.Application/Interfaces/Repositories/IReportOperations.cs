using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface IReportOperations
    {
        Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default);
        Task<List<OrderDetail>> GetOrderDetailsAsync(CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPaymentsAsync(CancellationToken cancellationToken = default);
        Task<List<Ticket>> GetTicketsAsync(CancellationToken cancellationToken = default);
        Task<List<TicketType>> GetTicketTypesAsync(CancellationToken cancellationToken = default);
        Task<List<User>> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<List<UserProfile>> GetUserProfilesAsync(CancellationToken cancellationToken = default);
        Task<List<EntryLog>> GetEntryLogsAsync(CancellationToken cancellationToken = default);
        Task<List<Incident>> GetIncidentsAsync(CancellationToken cancellationToken = default);
    }
}
