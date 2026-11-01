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
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Voucher> _voucherRepo;
        private readonly IRepository<Domain.Entities.UserProfile> _userProfileRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ISender _sender;

        public CheckoutOnlineHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Voucher> voucherRepo,
            IRepository<Domain.Entities.UserProfile> userProfileRepo,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ISender sender)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _voucherRepo = voucherRepo;
            _userProfileRepo = userProfileRepo;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _sender = sender;
        }

        public async Task<CheckoutOnlineResponse> Handle(CheckoutOnlineCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Items.Count == 0 || request.Items.Any(item => item.Quantity <= 0))
                throw new ArgumentException("Giỏ hàng phải có vé với số lượng lớn hơn 0.");
            decimal totalAmount = 0;
            var validatedItems = new List<(Domain.Entities.TicketType Type, int Quantity)>();

            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken);
                if (ticketType == null)
                    throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                totalAmount += ticketType.Price * item.Quantity;
                validatedItems.Add((ticketType, item.Quantity));
            }

            var hasMonthlyTicket = validatedItems.Any(item =>
                string.Equals(item.Type.TicketCategory, TicketCategoryEnum.VE_THANG.ToString(), StringComparison.OrdinalIgnoreCase));

            if (hasMonthlyTicket)
            {
                var profile = await _userProfileRepo.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
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
                var voucher = await _voucherRepo.FirstOrDefaultAsync(
                    VoucherValidityPolicy.UsableCodeAt(request.VoucherCode, now),
                    cancellationToken)
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

            await _orderRepo.AddAsync(order, cancellationToken);

            foreach (var vItem in validatedItems)
            {
                var detail = new Domain.Entities.OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ItemType = "TICKET",
                    ItemId = vItem.Type.Id,
                    Quantity = vItem.Quantity,
                    UnitPrice = vItem.Type.Price,
                    CreatedAt = createdAt
                };
                await _orderDetailRepo.AddAsync(detail, cancellationToken);
            }

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

            await _paymentRepo.AddAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                    UnitPrice = item.Type.Price
                }).ToList(),
                Tickets = confirmation?.Tickets ?? []
            };
        }
    }
}
