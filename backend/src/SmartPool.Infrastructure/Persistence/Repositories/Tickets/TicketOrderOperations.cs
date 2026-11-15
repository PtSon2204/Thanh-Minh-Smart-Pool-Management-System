using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Tickets;

public sealed class TicketOrderOperations(SmartPoolDbContext db) : ITicketOrderOperations
{
    public async Task CreateOrderAsync(Order order, IEnumerable<OrderDetail> details, Payment payment, CancellationToken cancellationToken)
    {
        var existingTx = db.Database.CurrentTransaction;
        await using var tx = existingTx == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await db.Orders.AddAsync(order, cancellationToken);
        await db.OrderDetails.AddRangeAsync(details, cancellationToken);
        await db.Payments.AddAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
    }

    public async Task CreateOfflineSaleAsync(Order order, IEnumerable<OrderDetail> details, Payment payment, IEnumerable<Ticket> tickets, CancellationToken cancellationToken)
    {
        var existingTx = db.Database.CurrentTransaction;
        await using var tx = existingTx == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await db.Orders.AddAsync(order, cancellationToken);
        await db.OrderDetails.AddRangeAsync(details, cancellationToken);
        await db.Payments.AddAsync(payment, cancellationToken);
        var ticketList = tickets.ToList();
        if (ticketList.Count > 0)
        {
            await db.Tickets.AddRangeAsync(ticketList, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.IsDeleted != true, cancellationToken);
    }

    public async Task<List<Order>> GetOrdersByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        return await db.Orders.AsNoTracking()
            .Where(o => idList.Contains(o.Id) && o.IsDeleted != true && o.Status != null && o.Status.ToUpper() == "COMPLETED")
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateOrderAsync(Order order, CancellationToken cancellationToken)
    {
        db.Orders.Update(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<OrderDetail>> GetOrderDetailsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await db.OrderDetails.Where(d => d.OrderId == orderId).ToListAsync(cancellationToken);
    }

    public async Task<List<OrderDetail>> GetTicketOrderDetailsByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken)
    {
        var idList = orderIds.ToList();
        return await db.OrderDetails.AsNoTracking()
            .Where(d => idList.Contains(d.OrderId) && d.ItemType.ToUpper() == "TICKET")
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderDetail>> GetTicketOrderDetailsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await db.OrderDetails.AsNoTracking()
            .Where(d => d.OrderId == orderId && d.ItemType.ToUpper() == "TICKET")
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetPaymentByTransactionRefAsync(string transactionRef, CancellationToken cancellationToken)
    {
        return await db.Payments.FirstOrDefaultAsync(p => p.TransactionRef == transactionRef, cancellationToken);
    }

    public async Task<Payment?> GetPaymentByOrderAndRefAsync(Guid orderId, string transactionRef, CancellationToken cancellationToken)
    {
        return await db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.TransactionRef == transactionRef, cancellationToken);
    }

    public async Task<List<Payment>> GetPaymentsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await db.Payments.Where(p => p.OrderId == orderId).ToListAsync(cancellationToken);
    }

    public async Task<List<Payment>> GetCompletedPaymentsByOrderIdAsync(Guid orderId, Guid? excludePaymentId, CancellationToken cancellationToken)
    {
        return await db.Payments
            .Where(p => p.OrderId == orderId && p.Id != excludePaymentId && p.Status == "COMPLETED")
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Payment>> GetDailySalesPaymentsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        return await db.Payments.AsNoTracking()
            .Where(p => p.Status != null && p.Status.ToUpper() == "COMPLETED" && p.PaymentTime >= startUtc && p.PaymentTime < endUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Payment>> GetPaymentsByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken)
    {
        var idList = orderIds.ToList();
        return await db.Payments.AsNoTracking()
            .Where(p => idList.Contains(p.OrderId) && p.Status != null && p.Status.ToUpper() == "COMPLETED")
            .ToListAsync(cancellationToken);
    }

    public async Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        await db.Payments.AddAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        db.Payments.Update(payment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveIncomingPaymentAsync(Payment? payment, CancellationToken cancellationToken)
    {
        if (payment != null)
        {
            await db.Payments.AddAsync(payment, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOrderWithPaymentAsync(Order order, Payment? payment, CancellationToken cancellationToken)
    {
        if (payment != null)
        {
            await db.Payments.AddAsync(payment, cancellationToken);
        }
        db.Orders.Update(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmOrderAndAddTicketsAsync(Order order, Payment pendingPayment, Payment? bankPayment, IEnumerable<Ticket> tickets, CancellationToken cancellationToken)
    {
        var existingTx = db.Database.CurrentTransaction;
        await using var tx = existingTx == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (bankPayment != null)
        {
            await db.Payments.AddAsync(bankPayment, cancellationToken);
        }
        db.Payments.Update(pendingPayment);
        db.Orders.Update(order);
        var ticketList = tickets.ToList();
        if (ticketList.Count > 0)
        {
            await db.Tickets.AddRangeAsync(ticketList, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
    }

    public async Task<Ticket?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> TicketCodeExistsAsync(string code, Guid? excludeTicketId, CancellationToken cancellationToken)
    {
        if (excludeTicketId.HasValue)
            return await db.Tickets.AnyAsync(t => t.QrCode == code && t.Id != excludeTicketId.Value, cancellationToken);
        return await db.Tickets.AnyAsync(t => t.QrCode == code, cancellationToken);
    }

    public async Task<List<Ticket>> GetTicketsByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Tickets.AsNoTracking()
            .Where(t => t.UserId == userId && t.IsDeleted != true)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        db.Tickets.Update(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EntryLog>> GetEntryLogsByTicketIdsAsync(IEnumerable<Guid> ticketIds, CancellationToken cancellationToken)
    {
        var idList = ticketIds.ToList();
        return await db.EntryLogs.AsNoTracking()
            .Where(e => e.TicketId.HasValue && idList.Contains(e.TicketId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EntryLog>> GetAllowedEntryLogsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        return await db.EntryLogs.AsNoTracking()
            .Where(e => e.TicketId.HasValue
                && e.ScanTime >= startUtc
                && e.ScanTime < endUtc
                && e.Status != null
                && e.Status.ToUpper() == "ALLOWED")
            .ToListAsync(cancellationToken);
    }
}
