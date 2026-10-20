using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IOrderPaymentOperations
{
    Task<T> ExecuteLockedAsync<T>(Guid orderId,
        Func<Order?, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task<int> ExpireDueOrdersAsync(CancellationToken cancellationToken = default);

    void UpdatePayment(Payment payment);
}
