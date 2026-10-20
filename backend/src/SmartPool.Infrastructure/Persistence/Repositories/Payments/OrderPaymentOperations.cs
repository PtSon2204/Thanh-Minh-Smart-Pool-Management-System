using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Helpers;
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
