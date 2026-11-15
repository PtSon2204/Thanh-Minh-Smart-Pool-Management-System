using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Notifications.Models;
using SmartPool.Application.Features.Notifications.Services;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderHandler : IRequestHandler<ConfirmPendingOrderCommand, ConfirmPendingOrderResponse>
    {
        private readonly ITicketTypeOperations _ticketTypeOperations;
        private readonly ITicketOrderOperations _ticketOrderOperations;
        private readonly IOrderPaymentOperations _orderPayments;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeProvider _timeProvider;
        private readonly IUserOperations? _userOperations;
        private readonly IEmailService? _emailService;
        private readonly IEmailHistoryStore? _historyStore;
        private readonly ILogger<ConfirmPendingOrderHandler>? _logger;

        public ConfirmPendingOrderHandler(
            ITicketTypeOperations ticketTypeOperations,
            ITicketOrderOperations ticketOrderOperations,
            IOrderPaymentOperations orderPayments,
            IMemoryCache memoryCache,
            TimeProvider timeProvider,
            IUserOperations? userOperations = null,
            IEmailService? emailService = null,
            IEmailHistoryStore? historyStore = null,
            ILogger<ConfirmPendingOrderHandler>? logger = null)
        {
            _ticketTypeOperations = ticketTypeOperations;
            _ticketOrderOperations = ticketOrderOperations;
            _orderPayments = orderPayments;
            _memoryCache = memoryCache;
            _timeProvider = timeProvider;
            _userOperations = userOperations;
            _emailService = emailService;
            _historyStore = historyStore;
            _logger = logger;
        }

        public async Task<ConfirmPendingOrderResponse> Handle(ConfirmPendingOrderCommand request, CancellationToken cancellationToken)
        {
            var pendingPayment = await _ticketOrderOperations.GetPaymentByTransactionRefAsync(
                request.TransactionRef,
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
            var pendingPayment = await _ticketOrderOperations.GetPaymentByOrderAndRefAsync(
                order.Id, request.TransactionRef, cancellationToken);
            if (pendingPayment == null)
                return new ConfirmPendingOrderResponse { Success = false, Message = "TransactionRef not found." };
            if (request.ActualAmount < 0 || (request.ActualAmount == 0 && order.FinalAmount > 0))
                return new ConfirmPendingOrderResponse { Success = false, Message = "Invalid incoming amount." };

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expired = OnlineOrderPaymentPolicy.TryExpire(order, pendingPayment, now);
            if (expired) _orderPayments.UpdatePayment(pendingPayment);
            if (!string.IsNullOrEmpty(request.BankReferenceCode))
            {
                var duplicate = await _ticketOrderOperations.GetPaymentByTransactionRefAsync(
                    request.BankReferenceCode,
                    cancellationToken);

                if (duplicate != null)
                {
                    if (expired) await _ticketOrderOperations.SaveChangesAsync(cancellationToken);
                    return new ConfirmPendingOrderResponse
                    {
                        Success = true,
                        Message = "Already processed (idempotent).",
                        OrderId = order.Id
                    };
                }
            }

            Payment? bankPayment = null;
            if (request.ActualAmount > 0)
            {
                bankPayment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Amount = request.ActualAmount,
                    PaymentMethod = pendingPayment.PaymentMethod,
                    TransactionRef = request.BankReferenceCode ?? $"BANK-{Guid.NewGuid():N}",
                    Status = PaymentStatusEnum.COMPLETED.ToString(),
                    PaymentTime = now,
                    CreatedAt = now
                };
            }

            if (order.Status is "CANCELLED" or "COMPLETED")
            {
                // Persist additional/late receipts for reconciliation, without issuing tickets again.
                await _ticketOrderOperations.SaveIncomingPaymentAsync(bankPayment, cancellationToken);
                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    OrderId = order.Id,
                    Message = order.Status == "CANCELLED"
                        ? "Order cancelled. Incoming payment recorded for reconciliation; no tickets issued."
                        : "Order already completed. Incoming payment recorded."
                };
            }

            var completedPayments = await _ticketOrderOperations.GetCompletedPaymentsByOrderIdAsync(
                order.Id, pendingPayment.Id, cancellationToken);

            decimal totalPaid = completedPayments.Sum(p => p.Amount) + request.ActualAmount;

            if (totalPaid < order.FinalAmount)
            {
                order.Status = "PARTIAL";
                order.UpdatedAt = now;
                await _ticketOrderOperations.UpdateOrderWithPaymentAsync(order, bankPayment, cancellationToken);

                return new ConfirmPendingOrderResponse
                {
                    Success = true,
                    OrderId = order.Id,
                    Message = $"Underpayment. Expected: {order.FinalAmount}, Total received so far: {totalPaid}, Remaining: {order.FinalAmount - totalPaid}"
                };
            }

            pendingPayment.Status = PaymentStatusEnum.COMPLETED.ToString();
            pendingPayment.PaymentTime = now;

            order.Status = OrderStatusEnum.COMPLETED.ToString();
            order.UpdatedAt = now;

            var response = new ConfirmPendingOrderResponse
            {
                Success = true,
                OrderId = order.Id,
                Message = "Confirmed and tickets generated."
            };

            var orderDetails = await _ticketOrderOperations.GetOrderDetailsByOrderIdAsync(order.Id, cancellationToken);

            DateTime issueDate = _timeProvider.GetUtcNow().UtcDateTime;
            var tickets = new List<Domain.Entities.Ticket>();
            var ticketTypesMap = new Dictionary<Guid, Domain.Entities.TicketType>();

            foreach (var detail in orderDetails)
            {
                var ticketType = await _ticketTypeOperations.GetByIdAsync(detail.ItemId, cancellationToken);
                if (ticketType == null) continue;
                ticketTypesMap[ticketType.Id] = ticketType;

                for (int i = 0; i < detail.Quantity; i++)
                {
                    int? resolvedEntries = detail.CustomEntries ?? (ticketType.TicketCategory == nameof(TicketCategoryEnum.VE_LUOT) ? (ticketType.TotalEntries ?? 1) : null);
                    int? remainingEntries = ticketType.TicketCategory == nameof(TicketCategoryEnum.VE_LUOT)
                        ? resolvedEntries
                        : null;

                    DateTime? expiryDate = TicketValidityPolicy.GetExpiryDate(issueDate, ticketType.TicketCategory, resolvedEntries);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = order.UserId,
                        QrCode = await GenerateUniqueCodeAsync(cancellationToken),
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        RemainingEntries = remainingEntries,
                        Status = TicketStatusEnum.ACTIVE.ToString(),
                        CreatedAt = now
                    };

                    tickets.Add(ticket);

                    response.Tickets.Add(new TicketInfo
                    {
                        Id = ticket.Id,
                        QrCode = ticket.QrCode,
                        ExpiryDate = ticket.ExpiryDate,
                        TicketCategory = ticketType.TicketCategory ?? "",
                        RemainingEntries = ticket.RemainingEntries
                    });
                }
            }

            await _ticketOrderOperations.ConfirmOrderAndAddTicketsAsync(order, pendingPayment, bankPayment, tickets, cancellationToken);

            _memoryCache.Set($"OrderTickets_{order.Id}", response.Tickets, TimeSpan.FromMinutes(10));

            // Gửi email xác nhận kèm mã vé cho khách hàng (non-blocking / fault-tolerant)
            await TrySendConfirmationEmailAsync(order, tickets, ticketTypesMap, cancellationToken);

            return response;
        }

        private async Task TrySendConfirmationEmailAsync(
            Order order,
            IReadOnlyList<Domain.Entities.Ticket> tickets,
            Dictionary<Guid, Domain.Entities.TicketType> ticketTypes,
            CancellationToken cancellationToken)
        {
            if (_emailService == null || _userOperations == null || tickets.Count == 0)
            {
                return;
            }

            try
            {
                User? user = null;
                if (order.UserId.HasValue)
                {
                    user = await _userOperations.GetByIdAsync(order.UserId.Value, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
                {
                    user = await _userOperations.GetByPhoneOrUsernameAsync(order.CustomerPhone, cancellationToken);
                }

                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    return;
                }

                var emailTickets = new List<TicketEmailItemDto>();
                foreach (var ticket in tickets)
                {
                    ticketTypes.TryGetValue(ticket.TicketTypeId, out var tt);
                    var isVeLuot = string.Equals(tt?.TicketCategory, nameof(TicketCategoryEnum.VE_LUOT), StringComparison.OrdinalIgnoreCase);

                    string details = isVeLuot
                        ? $"Số lượt: {ticket.RemainingEntries ?? 1} lượt" + (ticket.ExpiryDate.HasValue ? $" | Hạn dùng: {ticket.ExpiryDate.Value.AddHours(7):dd/MM/yyyy}" : "")
                        : $"Hạn sử dụng: {(ticket.ExpiryDate.HasValue ? ticket.ExpiryDate.Value.AddHours(7).ToString("dd/MM/yyyy") : "30 ngày")}";

                    emailTickets.Add(new TicketEmailItemDto
                    {
                        TicketCode = ticket.QrCode,
                        TicketTypeName = tt?.Name ?? "Vé bơi",
                        Category = tt?.TicketCategory ?? (isVeLuot ? "VE_LUOT" : "VE_THANG"),
                        RemainingEntries = ticket.RemainingEntries,
                        ExpiryDate = ticket.ExpiryDate,
                        Details = details
                    });
                }

                var recipientName = user.UserProfile?.FullName;
                if (string.IsNullOrWhiteSpace(recipientName))
                {
                    recipientName = !string.IsNullOrWhiteSpace(order.CustomerName) ? order.CustomerName : (user.Username ?? "Quý khách");
                }

                var shortOrderRef = order.Id.ToString("N")[..8].ToUpperInvariant();
                var subject = $"[Bể bơi Thanh Minh] Xác nhận đặt vé thành công - Đơn hàng #{shortOrderRef}";
                var orderRef = $"#{shortOrderRef}";

                var htmlBody = EmailTemplateBuilder.BuildOrderTicketConfirmationHtml(
                    recipientName,
                    subject,
                    orderRef,
                    order.TotalAmount,
                    order.DiscountAmount ?? 0,
                    order.FinalAmount,
                    order.UpdatedAt ?? _timeProvider.GetUtcNow().UtcDateTime,
                    emailTickets);

                await _emailService.SendAsync(user.Email, subject, htmlBody, cancellationToken);

                if (_historyStore != null)
                {
                    await _historyStore.AddLogAsync(new EmailHistoryItem
                    {
                        Recipient = user.Email,
                        RecipientName = recipientName,
                        Subject = subject,
                        TemplateType = "ticket_confirmation",
                        Status = "Thành công",
                        SentAt = _timeProvider.GetUtcNow().UtcDateTime,
                        HtmlBody = htmlBody
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Không thể gửi email xác nhận vé cho đơn hàng {OrderId}. Lỗi: {Message}", order.Id, ex.Message);
            }
        }

        /// <summary>Sinh mã 6 chữ số duy nhất, retry tối đa 10 lần nếu trùng.</summary>
        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var code = TicketCodeGenerator.Generate();
                var exists = await _ticketOrderOperations.TicketCodeExistsAsync(code, null, cancellationToken);
                if (!exists)
                    return code;
            }
            throw new InvalidOperationException("Không thể sinh mã vé duy nhất sau 10 lần thử. Vui lòng thử lại.");
        }
    }
}
