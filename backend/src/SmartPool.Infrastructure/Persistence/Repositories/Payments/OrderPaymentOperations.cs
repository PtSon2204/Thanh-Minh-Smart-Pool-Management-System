using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyOrders;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Payments;

public sealed class OrderPaymentOperations(SmartPoolDbContext context, TimeProvider timeProvider)
    : IOrderPaymentOperations
{
    public void UpdatePayment(Payment payment)
    {
        var tracked = context.Payments.Local.FirstOrDefault(entity => entity.Id == payment.Id);
        if (tracked == null) context.Payments.Update(payment);
        else context.Entry(tracked).CurrentValues.SetValues(payment);
    }

    public async Task<MyOrdersResponse> GetCustomerOrdersAsync(Guid userId, string? status, int pageIndex,
        int pageSize, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = status?.Trim().ToUpperInvariant();
        var query = context.Orders.AsNoTracking()
            .Where(order => order.UserId == userId && order.IsDeleted != true
                && order.Payments.Any(payment => payment.PaymentMethod == "TRANSFER"
                    && payment.TransactionRef != null && payment.TransactionRef.StartsWith("SP")
                    && payment.TransactionRef.Length == 8));

        if (normalizedStatus == "WAITING")
            query = query.Where(order => order.Status == "PENDING" || order.Status == "PARTIAL");
        else if (!string.IsNullOrEmpty(normalizedStatus) && normalizedStatus != "ALL")
            query = query.Where(order => order.Status == normalizedStatus);

        var totalCount = await query.CountAsync(cancellationToken);
        var unpaidCount = await GetCustomerUnpaidOrderCountAsync(userId, cancellationToken);
        var orders = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(order => new MyOrderSummaryDto
            {
                OrderId = order.Id,
                Status = order.Status ?? "PENDING",
                CreatedAt = order.CreatedAt,
                Subtotal = order.TotalAmount,
                DiscountAmount = order.DiscountAmount ?? 0,
                FinalAmount = order.FinalAmount,
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(order => order.OrderId).ToArray();
        if (orderIds.Length > 0)
        {
            var paidAmounts = await context.Payments.AsNoTracking()
                .Where(payment => orderIds.Contains(payment.OrderId) && payment.Status == "COMPLETED"
                    && !(payment.PaymentMethod == "TRANSFER" && payment.TransactionRef != null
                        && payment.TransactionRef.StartsWith("SP") && payment.TransactionRef.Length == 8))
                .GroupBy(payment => payment.OrderId)
                .Select(group => new { OrderId = group.Key, Amount = group.Sum(payment => payment.Amount) })
                .ToDictionaryAsync(item => item.OrderId, item => item.Amount, cancellationToken);

            var itemsByOrder = await (from detail in context.OrderDetails.AsNoTracking()
                                      join ticketType in context.TicketTypes.AsNoTracking()
                                          on detail.ItemId equals ticketType.Id
                                      where orderIds.Contains(detail.OrderId) && detail.ItemType == "TICKET"
                                      select new
                                      {
                                          detail.OrderId,
                                          Item = new OnlineOrderItemDto
                                          {
                                              TicketTypeId = detail.ItemId,
                                              Name = ticketType.Name,
                                              Quantity = detail.Quantity,
                                              UnitPrice = detail.UnitPrice
                                          }
                                      }).ToListAsync(cancellationToken);

            foreach (var order in orders)
            {
                order.ExpiresAtUtc = order.CreatedAt.HasValue
                    ? DateTime.SpecifyKind(order.CreatedAt.Value, DateTimeKind.Utc).Add(OnlineOrderPaymentPolicy.Lifetime)
                    : null;
                order.PaidAmount = paidAmounts.GetValueOrDefault(order.OrderId);
                order.RemainingAmount = Math.Max(0, order.FinalAmount - order.PaidAmount);
                order.Items = itemsByOrder.Where(item => item.OrderId == order.OrderId)
                    .Select(item => item.Item).ToList();
            }
        }

        return new MyOrdersResponse
        {
            Items = orders,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            UnpaidCount = unpaidCount
        };
    }

    public Task<int> GetCustomerUnpaidOrderCountAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.Subtract(OnlineOrderPaymentPolicy.Lifetime);
        return context.Orders.AsNoTracking()
            .Where(order => order.UserId == userId && order.IsDeleted != true
                && (order.Status == "PENDING" || order.Status == "PARTIAL")
                && order.CreatedAt > cutoff
                && order.Payments.Any(payment => payment.PaymentMethod == "TRANSFER"
                    && payment.TransactionRef != null && payment.TransactionRef.StartsWith("SP")
                    && payment.TransactionRef.Length == 8))
            .CountAsync(cancellationToken);
    }

    public async Task<T> ExecuteLockedAsync<T>(Guid orderId,
        Func<Order?, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // The same PostgreSQL row lock serializes webhook confirmation, status checks and expiry.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM smart_pool.orders WHERE id = {orderId} FOR UPDATE", cancellationToken);

        var order = await context.Orders.FindAsync([orderId], cancellationToken);
        if (order != null) await context.Entry(order).ReloadAsync(cancellationToken);
        var result = await action(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<int> ExpireDueOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.Subtract(OnlineOrderPaymentPolicy.Lifetime);
        var ids = await context.Orders.AsNoTracking()
            .Where(order => order.IsDeleted != true && (order.Status == "PENDING" || order.Status == "PARTIAL")
                && order.CreatedAt <= cutoff
                && order.Payments.Any(payment => payment.PaymentMethod == "TRANSFER"
                    && payment.TransactionRef != null && payment.TransactionRef.StartsWith("SP")
                    && payment.TransactionRef.Length == 8))
            .OrderBy(order => order.CreatedAt).Select(order => order.Id).Take(100)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var id in ids)
        {
            expired += await ExecuteLockedAsync(id, async (order, token) =>
            {
                if (order == null) return 0;
                var payment = await context.Payments
                    .Where(payment => payment.OrderId == id && payment.PaymentMethod == "TRANSFER"
                        && payment.TransactionRef != null && payment.TransactionRef.StartsWith("SP")
                        && payment.TransactionRef.Length == 8)
                    .OrderBy(payment => payment.CreatedAt).FirstOrDefaultAsync(token);
                if (!OnlineOrderPaymentPolicy.TryExpire(order, payment, timeProvider.GetUtcNow().UtcDateTime))
                    return 0;
                await context.SaveChangesAsync(token);
                return 1;
            }, cancellationToken);
        }
        return expired;
    }
}
