using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderHandler : IRequestHandler<ConfirmPendingOrderCommand, ConfirmPendingOrderResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IOrderPaymentOperations _orderPayments;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeProvider _timeProvider;

        public ConfirmPendingOrderHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IOrderPaymentOperations orderPayments,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IUnitOfWork unitOfWork,
            IMemoryCache memoryCache,
            TimeProvider timeProvider)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderPayments = orderPayments;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _ticketRepo = ticketRepo;
            _unitOfWork = unitOfWork;
            _memoryCache = memoryCache;
            _timeProvider = timeProvider;
        }

        public async Task<ConfirmPendingOrderResponse> Handle(ConfirmPendingOrderCommand request, CancellationToken cancellationToken)
        {
            var pendingPayment = await _paymentRepo.FirstOrDefaultAsync(
                p => p.TransactionRef == request.TransactionRef,
                cancellationToken);

            if (pendingPayment == null)
            {
                return new ConfirmPendingOrderResponse { Success = false, Message = "TransactionRef not found." };
            }

            return await _orderPayments.ExecuteLockedAsync(pendingPayment.OrderId,
                (order, token) => ConfirmLockedAsync(request, order, token), cancellationToken);
        }

        private async Task<ConfirmPendingOrderResponse> ConfirmLockedAsync(ConfirmPendingOrderCommand request,
            Order? order, CancellationToken cancellationToken)
        {
            if (order == null)
            {
                return new ConfirmPendingOrderResponse { Success = false, Message = "Order not found." };
            }

            // Re-read after acquiring the lock; expiry or another webhook may have run in between.
            var pendingPayment = await _paymentRepo.FirstOrDefaultAsync(
                p => p.OrderId == order.Id && p.TransactionRef == request.TransactionRef, cancellationToken);
            if (pendingPayment == null)
                return new ConfirmPendingOrderResponse { Success = false, Message = "TransactionRef not found." };
            if (request.ActualAmount < 0 || (request.ActualAmount == 0 && order.FinalAmount > 0))
                return new ConfirmPendingOrderResponse { Success = false, Message = "Invalid incoming amount." };

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expired = OnlineOrderPaymentPolicy.TryExpire(order, pendingPayment, now);
            if (expired) _orderPayments.UpdatePayment(pendingPayment);
            if (!string.IsNullOrEmpty(request.BankReferenceCode))
            {
                var duplicate = await _paymentRepo.FirstOrDefaultAsync(
                    p => p.TransactionRef == request.BankReferenceCode,
                    cancellationToken);

                if (duplicate != null)
                {
                    if (expired) await _unitOfWork.SaveChangesAsync(cancellationToken);
                    return new ConfirmPendingOrderResponse
                    {
                        Success = true,
                        Message = "Already processed (idempotent).",
                        OrderId = order.Id
                    };
                }
            }

            if (request.ActualAmount > 0)
            {
                await _paymentRepo.AddAsync(new Domain.Entities.Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Amount = request.ActualAmount,
                    PaymentMethod = pendingPayment.PaymentMethod,
                    TransactionRef = request.BankReferenceCode ?? $"BANK-{Guid.NewGuid():N}",
                    Status = PaymentStatusEnum.COMPLETED.ToString(),
                    PaymentTime = now,
                    CreatedAt = now
                }, cancellationToken);
            }

            if (order.Status is "CANCELLED" or "COMPLETED")
            {
                // Persist additional/late receipts for reconciliation, without issuing tickets again.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    OrderId = order.Id,
                    Message = order.Status == "CANCELLED"
                        ? "Order cancelled. Incoming payment recorded for reconciliation; no tickets issued."
                        : "Order already completed. Incoming payment recorded."
                };
            }

            var completedPayments = await _paymentRepo.FindAsync(
                p => p.OrderId == order.Id && p.Id != pendingPayment.Id
                    && p.Status == PaymentStatusEnum.COMPLETED.ToString(),
                cancellationToken);

            decimal totalPaid = completedPayments.Sum(p => p.Amount) + request.ActualAmount;

            if (totalPaid < order.FinalAmount)
            {
                order.Status = "PARTIAL";
                order.UpdatedAt = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    OrderId = order.Id,
                    Message = $"Underpayment. Expected: {order.FinalAmount}, Total received so far: {totalPaid}, Remaining: {order.FinalAmount - totalPaid}"
                };
            }

            pendingPayment.Status = PaymentStatusEnum.COMPLETED.ToString();
            pendingPayment.PaymentTime = now;
            _orderPayments.UpdatePayment(pendingPayment);

            order.Status = OrderStatusEnum.COMPLETED.ToString();
            order.UpdatedAt = now;

            var response = new ConfirmPendingOrderResponse
            {
                Success = true,
                OrderId = order.Id,
                Message = "Confirmed and tickets generated."
            };

            var orderDetails = await _orderDetailRepo.FindAsync(od => od.OrderId == order.Id, cancellationToken);

            DateTime issueDate = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (var detail in orderDetails)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(detail.ItemId, cancellationToken);
                if (ticketType == null) continue;

                for (int i = 0; i < detail.Quantity; i++)
                {
                    DateTime expiryDate = TicketValidityPolicy.GetExpiryDate(issueDate, ticketType.TicketCategory);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = order.UserId,
                        QrCode = await GenerateUniqueCodeAsync(cancellationToken),
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        Status = TicketStatusEnum.ACTIVE.ToString(),
                        CreatedAt = now
                    };

                    await _ticketRepo.AddAsync(ticket, cancellationToken);

                    response.Tickets.Add(new TicketInfo
                    {
                        Id = ticket.Id,
                        QrCode = ticket.QrCode,
                        ExpiryDate = ticket.ExpiryDate,
                        TicketCategory = ticketType.TicketCategory ?? ""
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _memoryCache.Set($"OrderTickets_{order.Id}", response.Tickets, TimeSpan.FromMinutes(10));

            return response;
        }

        /// <summary>Sinh mã 6 chữ số duy nhất, retry tối đa 10 lần nếu trùng.</summary>
        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var code = TicketCodeGenerator.Generate();
                var exists = await _ticketRepo.FirstOrDefaultAsync(t => t.QrCode == code, cancellationToken);
                if (exists == null)
                    return code;
            }
            throw new InvalidOperationException("Không thể sinh mã vé duy nhất sau 10 lần thử. Vui lòng thử lại.");
        }
    }
}
