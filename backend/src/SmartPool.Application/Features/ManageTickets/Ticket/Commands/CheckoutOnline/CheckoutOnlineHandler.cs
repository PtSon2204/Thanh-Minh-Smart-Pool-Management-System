using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline
{
    public class CheckoutOnlineHandler : IRequestHandler<CheckoutOnlineCommand, CheckoutOnlineResponse>
    {
        private readonly ITicketTypeOperations _ticketTypeOperations;
        private readonly ITicketOrderOperations _ticketOrderOperations;
        private readonly IVoucherOperations _voucherOperations;
        private readonly IUserOperations _userOperations;
        private readonly TimeProvider _timeProvider;
        private readonly ISender _sender;

        public CheckoutOnlineHandler(
            ITicketTypeOperations ticketTypeOperations,
            ITicketOrderOperations ticketOrderOperations,
            IVoucherOperations voucherOperations,
            IUserOperations userOperations,
            TimeProvider timeProvider,
            ISender sender)
        {
            _ticketTypeOperations = ticketTypeOperations;
            _ticketOrderOperations = ticketOrderOperations;
            _voucherOperations = voucherOperations;
            _userOperations = userOperations;
            _timeProvider = timeProvider;
            _sender = sender;
        }

        public async Task<CheckoutOnlineResponse> Handle(CheckoutOnlineCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Items.Count == 0 || request.Items.Any(item => item.Quantity <= 0))
                throw new ArgumentException("Giỏ hàng phải có vé với số lượng lớn hơn 0.");
            decimal totalAmount = 0;
            var validatedItems = new List<(Domain.Entities.TicketType Type, int Quantity, int? CustomEntries)>();

            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeOperations.GetByIdAsync(item.TicketTypeId, cancellationToken);
                if (ticketType == null)
                    throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                int entries = (item.RequestedEntries.HasValue && item.RequestedEntries.Value > 0) ? item.RequestedEntries.Value : 1;
                bool isVeLuot = string.Equals(ticketType.TicketCategory, nameof(TicketCategoryEnum.VE_LUOT), StringComparison.OrdinalIgnoreCase);
                int priceMultiplier = isVeLuot ? entries : 1;

                totalAmount += ticketType.Price * priceMultiplier * item.Quantity;
                validatedItems.Add((ticketType, item.Quantity, isVeLuot ? entries : null));
            }

            var hasMonthlyTicket = validatedItems.Any(item =>
                string.Equals(item.Type.TicketCategory, TicketCategoryEnum.VE_THANG.ToString(), StringComparison.OrdinalIgnoreCase));

            if (hasMonthlyTicket)
            {
                var profile = await _userOperations.GetUserProfileByUserIdAsync(request.UserId, cancellationToken);
                if (profile == null || string.IsNullOrWhiteSpace(profile.FullName?.Trim()))
                {
                    throw new InvalidOperationException("Vui lòng cập nhật họ và tên trong hồ sơ cá nhân để mua vé tháng.");
                }
            }

            decimal discountAmount = 0;
            Guid? appliedVoucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var voucher = await _voucherOperations.GetUsableVoucherByCodeAsync(request.VoucherCode, now, cancellationToken)
                    ?? throw new InvalidOperationException($"Mã giảm giá '{request.VoucherCode}' không hợp lệ hoặc đã hết hạn.");

                if (voucher.MinOrderValue.HasValue && totalAmount < voucher.MinOrderValue.Value)
                    throw new InvalidOperationException(
                        $"Đơn hàng tối thiểu {voucher.MinOrderValue:N0}đ để dùng mã này.");

                discountAmount = voucher.DiscountType == "PERCENTAGE"
                    ? (totalAmount * voucher.DiscountValue) / 100
                    : voucher.DiscountValue;
                
                appliedVoucherId = voucher.Id;
            }

            decimal finalAmount = totalAmount - discountAmount;
            if (finalAmount < 0) finalAmount = 0;
            discountAmount = totalAmount - finalAmount;
            var createdAt = _timeProvider.GetUtcNow().UtcDateTime;

            // SePay webhook looks for "SPxxxxxx"
            var transactionRef = "SP" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            var order = new Domain.Entities.Order
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                VoucherId = appliedVoucherId,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                Status = OrderStatusEnum.PENDING.ToString(),
                CreatedAt = createdAt,
                IsDeleted = false
            };

            var orderDetails = validatedItems.Select(vItem => new Domain.Entities.OrderDetail
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ItemType = "TICKET",
                ItemId = vItem.Type.Id,
                Quantity = vItem.Quantity,
                UnitPrice = vItem.Type.Price * (vItem.CustomEntries ?? 1),
                CustomEntries = vItem.CustomEntries,
                CreatedAt = createdAt
            }).ToList();

            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PaymentMethod = "TRANSFER",
                Amount = finalAmount,
                TransactionRef = transactionRef,
                Status = PaymentStatusEnum.PENDING.ToString(),
                CreatedAt = createdAt
            };

            await _ticketOrderOperations.CreateOrderAsync(order, orderDetails, payment, cancellationToken);

            // A fully discounted order needs no bank transfer or QR waiting period.
            ConfirmPendingOrderResponse? confirmation = null;
            if (finalAmount == 0)
            {
                confirmation = await _sender.Send(new ConfirmPendingOrderCommand
                {
                    TransactionRef = transactionRef,
                    ActualAmount = 0
                }, cancellationToken);
                if (!confirmation.Success) throw new InvalidOperationException(confirmation.Message);
            }

            return new CheckoutOnlineResponse
            {
                Success = true,
                Message = "Tạo đơn hàng chờ thanh toán thành công.",
                OrderId = order.Id,
                TotalAmount = finalAmount,
                TransactionRef = transactionRef,
                Status = confirmation == null ? "PENDING" : "COMPLETED",
                Subtotal = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                RemainingAmount = finalAmount,
                ExpiresAtUtc = OnlineOrderPaymentPolicy.GetExpiresAtUtc(order, payment),
                ServerTimeUtc = _timeProvider.GetUtcNow().UtcDateTime,
                Items = validatedItems.Select(item => new OnlineOrderItemDto
                {
                    TicketTypeId = item.Type.Id,
                    Name = item.Type.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.Type.Price * (item.CustomEntries ?? 1)
                }).ToList(),
                Tickets = confirmation?.Tickets ?? []
            };
        }
    }
}
