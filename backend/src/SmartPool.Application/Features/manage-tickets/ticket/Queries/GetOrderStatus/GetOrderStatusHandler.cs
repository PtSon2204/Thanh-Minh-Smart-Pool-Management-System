using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;
using SmartPool.Application.Common.Helpers;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus
{
    public class GetOrderStatusHandler : IRequestHandler<GetOrderStatusQuery, GetOrderStatusResponse>
    {
        private readonly IOrderPaymentOperations _orderPayments;
        private readonly IRepository<Payment> _paymentRepo;
        private readonly IRepository<OrderDetail> _detailRepo;
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly IMemoryCache _memoryCache;

        public GetOrderStatusHandler(
            IOrderPaymentOperations orderPayments,
            IRepository<Payment> paymentRepo,
            IRepository<OrderDetail> detailRepo,
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            IMemoryCache memoryCache)
        {
            _orderPayments = orderPayments;
            _paymentRepo = paymentRepo;
            _detailRepo = detailRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _memoryCache = memoryCache;
        }

        public async Task<GetOrderStatusResponse> Handle(GetOrderStatusQuery request, CancellationToken cancellationToken)
        {
            return await _orderPayments.ExecuteLockedAsync(request.OrderId,
                (order, token) => ReadLockedAsync(request, order, token), cancellationToken);
        }

        private async Task<GetOrderStatusResponse> ReadLockedAsync(GetOrderStatusQuery request,
            Order? order, CancellationToken cancellationToken)
        {
            if (order == null || order.IsDeleted == true ||
                (request.UserId.HasValue && order.UserId != request.UserId))
                throw new KeyNotFoundException("Order not found.");

            var payments = await _paymentRepo.FindAsync(
                p => p.OrderId == order.Id,
                cancellationToken);
            var originalPayment = payments.OrderBy(p => p.CreatedAt)
                .FirstOrDefault(p => OnlineOrderPaymentPolicy.IsCheckoutReference(p.TransactionRef));
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (OnlineOrderPaymentPolicy.TryExpire(order, originalPayment, now))
            {
                if (originalPayment != null) _orderPayments.UpdatePayment(originalPayment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            decimal paidAmount = payments.Where(p => p.Id != originalPayment?.Id && p.Status == "COMPLETED")
                .Sum(p => p.Amount);
            decimal remainingAmount = Math.Max(0, order.FinalAmount - paidAmount);

            var response = new GetOrderStatusResponse
            {
                Status = order.Status ?? "PENDING",
                OrderId = order.Id,
                TransactionRef = originalPayment?.TransactionRef ?? string.Empty,
                Subtotal = order.TotalAmount,
                DiscountAmount = order.DiscountAmount ?? 0,
                FinalAmount = order.FinalAmount,
                ExpiresAtUtc = OnlineOrderPaymentPolicy.GetExpiresAtUtc(order, originalPayment),
                ServerTimeUtc = now,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount
            };

            if (request.UserId.HasValue)
            {
                var details = await _detailRepo.FindAsync(d => d.OrderId == order.Id && d.ItemType == "TICKET", cancellationToken);
                foreach (var detail in details)
                {
                    var type = await _ticketTypeRepo.GetByIdAsync(detail.ItemId, cancellationToken);
                    response.Items.Add(new OnlineOrderItemDto
                    {
                        TicketTypeId = detail.ItemId,
                        Name = type?.Name ?? "Vé bơi",
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice
                    });
                }
            }

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

