using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyOrders;

public sealed class GetMyOrdersHandler(IOrderPaymentOperations operations)
    : IRequestHandler<GetMyOrdersQuery, MyOrdersResponse>,
      IRequestHandler<GetMyOrdersSummaryQuery, MyOrderSummaryResponse>
{
    public Task<MyOrdersResponse> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken) =>
        operations.GetCustomerOrdersAsync(request.UserId, request.Status, request.PageIndex, request.PageSize,
            cancellationToken);

    public async Task<MyOrderSummaryResponse> Handle(GetMyOrdersSummaryQuery request,
        CancellationToken cancellationToken) =>
        new()
        {
            UnpaidCount = await operations.GetCustomerUnpaidOrderCountAsync(request.UserId, cancellationToken)
        };
}
