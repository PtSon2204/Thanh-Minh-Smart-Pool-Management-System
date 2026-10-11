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
        private readonly IRepository<Payment> _paymentRepo;
        private readonly IMemoryCache _memoryCache;

        public GetOrderStatusHandler(
            IRepository<Order> orderRepo,
            IRepository<Payment> paymentRepo,
            IMemoryCache memoryCache)
        {
            _orderRepo = orderRepo;
            _paymentRepo = paymentRepo;
            _memoryCache = memoryCache;
        }

        public async Task<GetOrderStatusResponse> Handle(GetOrderStatusQuery request, CancellationToken cancellationToken)
        {
            var order = await _orderRepo.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            // Tính tổng số tiền đã nhận qua các giao dịch thực tế (status = COMPLETED)
            // Bỏ qua Payment gốc PENDING được tạo lúc đặt hàng (TransactionRef bắt đầu bằng "SP")
            var payments = await _paymentRepo.FindAsync(
                p => p.OrderId == order.Id && p.Status == "COMPLETED",
                cancellationToken);

            decimal paidAmount = payments.Sum(p => p.Amount);
            decimal remainingAmount = Math.Max(0, order.FinalAmount - paidAmount);

            var response = new GetOrderStatusResponse
            {
                Status = order.Status ?? "PENDING",
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount
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

