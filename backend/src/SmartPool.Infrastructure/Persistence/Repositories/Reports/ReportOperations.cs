using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Reports
{
    public class ReportOperations : IReportOperations
    {
        private readonly SmartPoolDbContext _context;

        public ReportOperations(SmartPoolDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default)
            => await _context.Orders.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<OrderDetail>> GetOrderDetailsAsync(CancellationToken cancellationToken = default)
            => await _context.OrderDetails.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<Payment>> GetPaymentsAsync(CancellationToken cancellationToken = default)
            => await _context.Payments.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<Ticket>> GetTicketsAsync(CancellationToken cancellationToken = default)
            => await _context.Tickets.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<TicketType>> GetTicketTypesAsync(CancellationToken cancellationToken = default)
            => await _context.TicketTypes.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<User>> GetUsersAsync(CancellationToken cancellationToken = default)
            => await _context.Users.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<UserProfile>> GetUserProfilesAsync(CancellationToken cancellationToken = default)
            => await _context.UserProfiles.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<EntryLog>> GetEntryLogsAsync(CancellationToken cancellationToken = default)
            => await _context.EntryLogs.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<List<Incident>> GetIncidentsAsync(CancellationToken cancellationToken = default)
            => await _context.Incidents.AsNoTracking().ToListAsync(cancellationToken);
    }
}
