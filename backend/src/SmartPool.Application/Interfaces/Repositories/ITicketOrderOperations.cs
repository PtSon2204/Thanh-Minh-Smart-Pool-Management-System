using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface ITicketOrderOperations
{
    // Orders & Checkout
    Task CreateOrderAsync(Order order, IEnumerable<OrderDetail> details, Payment payment, CancellationToken cancellationToken);
    Task CreateOfflineSaleAsync(Order order, IEnumerable<OrderDetail> details, Payment payment, IEnumerable<Ticket> tickets, CancellationToken cancellationToken);
    Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Order>> GetOrdersByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    Task UpdateOrderAsync(Order order, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // OrderDetails
    Task<List<OrderDetail>> GetOrderDetailsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task<List<OrderDetail>> GetTicketOrderDetailsByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken);
    Task<List<OrderDetail>> GetTicketOrderDetailsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    // Payments
    Task<Payment?> GetPaymentByTransactionRefAsync(string transactionRef, CancellationToken cancellationToken);
    Task<Payment?> GetPaymentByOrderAndRefAsync(Guid orderId, string transactionRef, CancellationToken cancellationToken);
    Task<List<Payment>> GetPaymentsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task<List<Payment>> GetCompletedPaymentsByOrderIdAsync(Guid orderId, Guid? excludePaymentId, CancellationToken cancellationToken);
    Task<List<Payment>> GetDailySalesPaymentsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);
    Task<List<Payment>> GetPaymentsByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken);
    Task UpdatePaymentAsync(Payment payment, CancellationToken cancellationToken);
    Task SaveIncomingPaymentAsync(Payment? payment, CancellationToken cancellationToken);
    Task UpdateOrderWithPaymentAsync(Order order, Payment? payment, CancellationToken cancellationToken);
    Task ConfirmOrderAndAddTicketsAsync(Order order, Payment pendingPayment, Payment? bankPayment, IEnumerable<Ticket> tickets, CancellationToken cancellationToken);

    // Tickets
    Task<Ticket?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TicketCodeExistsAsync(string code, Guid? excludeTicketId, CancellationToken cancellationToken);
    Task<List<Ticket>> GetTicketsByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken);

    // EntryLogs
    Task<List<EntryLog>> GetEntryLogsByTicketIdsAsync(IEnumerable<Guid> ticketIds, CancellationToken cancellationToken);
    Task<List<EntryLog>> GetAllowedEntryLogsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);
}
