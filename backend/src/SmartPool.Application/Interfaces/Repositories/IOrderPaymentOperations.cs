using SmartPool.Domain.Entities;
using SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyOrders;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IOrderPaymentOperations
{
    Task<T> ExecuteLockedAsync<T>(Guid orderId,
        Func<Order?, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task<int> ExpireDueOrdersAsync(CancellationToken cancellationToken = default);

    Task<MyOrdersResponse> GetCustomerOrdersAsync(Guid userId, string? status, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetCustomerUnpaidOrderCountAsync(Guid userId, CancellationToken cancellationToken = default);

    void UpdatePayment(Payment payment);
}
