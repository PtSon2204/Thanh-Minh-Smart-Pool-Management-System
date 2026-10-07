using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline
{
    public class CheckoutOnlineHandler : IRequestHandler<CheckoutOnlineCommand, CheckoutOnlineResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Voucher> _voucherRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly IMemoryCache _memoryCache;

        public CheckoutOnlineHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Voucher> voucherRepo,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            IMemoryCache memoryCache)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _voucherRepo = voucherRepo;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _memoryCache = memoryCache;
        }

        public async Task<CheckoutOnlineResponse> Handle(CheckoutOnlineCommand request, CancellationToken cancellationToken)
        {
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

            decimal discountAmount = 0;
            Guid? appliedVoucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var voucher = await _voucherRepo.FirstOrDefaultAsync(
                    v => v.Code == request.VoucherCode
                         && v.IsDeleted != true
                         && v.IsActive == true
                         && (v.StartDate == null || v.StartDate <= now)
                         && (v.EndDate == null || v.EndDate >= now),
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
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
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
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
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
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            await _paymentRepo.AddAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Caching for ticket generation logic later in ConfirmPendingOrderHandler
            // We just store the expected StartDate as UtcNow, so Webhook knows when it was ordered
            _memoryCache.Set($"OrderStartDate_{order.Id}", _timeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromDays(1));

            return new CheckoutOnlineResponse
            {
                Success = true,
                Message = "Tạo đơn hàng chờ thanh toán thành công.",
                OrderId = order.Id,
                TotalAmount = finalAmount,
                TransactionRef = transactionRef // We need to add this property!
            };
        }
    }
}
