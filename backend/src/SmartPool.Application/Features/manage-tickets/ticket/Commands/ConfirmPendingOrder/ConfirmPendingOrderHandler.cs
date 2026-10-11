using MediatR;
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
            // ── 1. Tìm Payment gốc (PENDING) dựa vào TransactionRef (SPxxxxxx) ──────────────
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

            // ── 2. Idempotency: Kiểm tra mã giao dịch ngân hàng đã được xử lý chưa ─────────
            // BankReferenceCode là mã giao dịch duy nhất từ ngân hàng (qua SePay).
            // Nếu SePay retry gọi Webhook nhiều lần cho cùng 1 giao dịch, ta chặn ở đây.
            if (!string.IsNullOrEmpty(request.BankReferenceCode))
            {
                var duplicate = await _paymentRepo.FirstOrDefaultAsync(
                    p => p.TransactionRef == request.BankReferenceCode,
                    cancellationToken);

                if (duplicate != null)
                {
                    // Giao dịch này đã được ghi nhận rồi → trả về thành công, không làm gì thêm
                    return new ConfirmPendingOrderResponse
                    {
                        Success = true,
                        Message = "Already processed (idempotent).",
                        OrderId = order.Id
                    };
                }
            }

            // ── 3. Lưu lịch sử giao dịch thực tế từ ngân hàng vào bảng Payment ─────────────
            // Mỗi lần SePay gọi Webhook = 1 lần chuyển khoản riêng biệt → 1 record Payment mới
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

            // ── 4. Cộng dồn tổng tiền đã nhận (bao gồm lần chuyển vừa rồi) ──────────────────
            // Lấy tất cả Payment COMPLETED của đơn này (không tính Payment PENDING gốc)
            var completedPayments = await _paymentRepo.FindAsync(
                p => p.OrderId == order.Id && p.Status == PaymentStatusEnum.COMPLETED.ToString(),
                cancellationToken);

            decimal totalPaid = completedPayments.Sum(p => p.Amount) + request.ActualAmount;

            // ── 5a. Chuyển khoản thiếu (Underpayment) ────────────────────────────────────────
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

            // ── 5b. Đã đủ tiền → kiểm tra order đã COMPLETED chưa (tránh sinh vé 2 lần) ─────
            if (order.Status == OrderStatusEnum.COMPLETED.ToString())
            {
                // Đơn hàng đã hoàn tất trước đó (có thể do Webhook đến trễ sau khi đã xử lý xong)
                // Không sinh vé thêm, chỉ trả về thành công
                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    Message = "Order already completed.",
                    OrderId = order.Id
                };
            }

            // ── 5c. Lần đầu đủ tiền → cập nhật trạng thái và sinh vé ─────────────────────────
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
                        QrCode = $"TKT-{Guid.NewGuid().ToString("N").ToUpper()}",
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

            // Lưu vé vào cache 10 phút để Frontend polling lấy được
            _memoryCache.Set($"OrderTickets_{order.Id}", response.Tickets, TimeSpan.FromMinutes(10));

            return response;
        }
    }
}
