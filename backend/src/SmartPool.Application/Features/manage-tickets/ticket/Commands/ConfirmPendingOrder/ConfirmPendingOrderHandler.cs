using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;
using SmartPool.Application.Common.Time;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderHandler : IRequestHandler<ConfirmPendingOrderCommand, ConfirmPendingOrderResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeProvider _timeProvider;

        public ConfirmPendingOrderHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IUnitOfWork unitOfWork,
            IMemoryCache memoryCache,
            TimeProvider timeProvider)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
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

            var order = await _orderRepo.GetByIdAsync(pendingPayment.OrderId, cancellationToken);
            if (order == null)
            {
                return new ConfirmPendingOrderResponse { Success = false, Message = "Order not found." };
            }
            if (!string.IsNullOrEmpty(request.BankReferenceCode))
            {
                var duplicate = await _paymentRepo.FirstOrDefaultAsync(
                    p => p.TransactionRef == request.BankReferenceCode,
                    cancellationToken);

                if (duplicate != null)
                {
                    return new ConfirmPendingOrderResponse
                    {
                        Success = true,
                        Message = "Already processed (idempotent).",
                        OrderId = order.Id
                    };
                }
            }

            var newPayment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = request.ActualAmount,
                PaymentMethod = pendingPayment.PaymentMethod,
                TransactionRef = request.BankReferenceCode ?? $"BANK-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                Status = PaymentStatusEnum.COMPLETED.ToString(),
                PaymentTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(newPayment, cancellationToken);

            var completedPayments = await _paymentRepo.FindAsync(
                p => p.OrderId == order.Id && p.Status == PaymentStatusEnum.COMPLETED.ToString(),
                cancellationToken);

            decimal totalPaid = completedPayments.Sum(p => p.Amount) + request.ActualAmount;

            if (totalPaid < order.FinalAmount)
            {
                order.Status = "PARTIAL";
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new ConfirmPendingOrderResponse
                {
                    Success = false,
                    Message = $"Underpayment. Expected: {order.FinalAmount}, Total received so far: {totalPaid}, Remaining: {order.FinalAmount - totalPaid}"
                };
            }

            if (order.Status == OrderStatusEnum.COMPLETED.ToString())
            {
                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    Message = "Order already completed.",
                    OrderId = order.Id
                };
            }

            pendingPayment.Status = PaymentStatusEnum.COMPLETED.ToString();
            pendingPayment.PaymentTime = DateTime.UtcNow;

            order.Status = OrderStatusEnum.COMPLETED.ToString();

            var response = new ConfirmPendingOrderResponse
            {
                Success = true,
                OrderId = order.Id,
                Message = "Confirmed and tickets generated."
            };

            var orderDetails = await _orderDetailRepo.FindAsync(od => od.OrderId == order.Id, cancellationToken);

            DateTime issueDate = _timeProvider.GetUtcNow().UtcDateTime;
            if (_memoryCache.TryGetValue($"OrderStartDate_{order.Id}", out DateTime cachedStartDate))
            {
                issueDate = cachedStartDate.ToUniversalTime();
            }

            foreach (var detail in orderDetails)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(detail.ItemId, cancellationToken);
                if (ticketType == null) continue;

                for (int i = 0; i < detail.Quantity; i++)
                {
                    DateTime expiryDate = ticketType.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString()
                        ? VietnamTimeBoundary.GetNextUtcDayBoundary(issueDate)
                        : issueDate.AddDays(ticketType.DurationDays ?? 30);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = order.UserId,
                        QrCode = await GenerateUniqueCodeAsync(cancellationToken),
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        Status = TicketStatusEnum.ACTIVE.ToString(),
                        CreatedAt = DateTime.UtcNow
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
