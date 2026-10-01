using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus
{
    public class GetOrderStatusHandler : IRequestHandler<GetOrderStatusQuery, GetOrderStatusResponse>
    {
        private readonly IRepository<Order> _orderRepo;
        private readonly IMemoryCache _memoryCache;

        public GetOrderStatusHandler(
            IRepository<Order> orderRepo,
            IMemoryCache memoryCache)
        {
            _orderRepo = orderRepo;
            _memoryCache = memoryCache;
        }

        public async Task<GetOrderStatusResponse> Handle(GetOrderStatusQuery request, CancellationToken cancellationToken)
        {
            var order = await _orderRepo.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            var response = new GetOrderStatusResponse
            {
                Status = order.Status ?? "PENDING"
            };

            if (response.Status == "COMPLETED")
            {
                if (_memoryCache.TryGetValue($"OrderTickets_{order.Id}", out List<TicketInfo>? tickets) && tickets != null)
                {
                    response.Tickets = tickets;
                }
            }

            return response;
        }
    }
}
