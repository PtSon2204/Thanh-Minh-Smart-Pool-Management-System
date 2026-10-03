using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder
{
    public class CreatePendingOrderHandler : IRequestHandler<CreatePendingOrderCommand, CreatePendingOrderResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.User> _userRepo;
        private readonly IRepository<Domain.Entities.Voucher> _voucherRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public CreatePendingOrderHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.User> userRepo,
            IRepository<Domain.Entities.Voucher> voucherRepo,
            IUnitOfWork unitOfWork,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _userRepo = userRepo;
            _voucherRepo = voucherRepo;
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public async Task<CreatePendingOrderResponse> Handle(CreatePendingOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                throw new ArgumentException("Giỏ hàng rỗng.");

            Guid? customerUserId = null;
            AccountInfo? accountInfo = null;

            // Xử lý Khách hàng nếu có SĐT
            if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
            {
                var existingUser = await _userRepo.FirstOrDefaultAsync(
                    u => u.Phone == request.CustomerPhone || u.Username == request.CustomerPhone,
                    cancellationToken);

                if (existingUser != null)
                {
                    customerUserId = existingUser.Id;
                }
                else
                {
                    var password = GenerateStrongPassword();

                    var newUser = new Domain.Entities.User
                    {
                        Id = Guid.NewGuid(),
                        Username = request.CustomerPhone,
                        Phone = request.CustomerPhone,
                        PasswordHash = password,
                        Status = UserStatusEnum.ACTIVE.ToString(),
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _userRepo.AddAsync(newUser, cancellationToken);
                    customerUserId = newUser.Id;

                    accountInfo = new AccountInfo
                    {
                        Username = request.CustomerPhone,
                        Password = password
                    };
                }
            }

            var transactionRef = "SP" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            decimal totalAmount = 0;
            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                decimal itemTotal = ticketType.Price * item.Quantity;
                totalAmount += itemTotal;
            }

            // Validate và áp dụng Voucher (nếu có)
            decimal discountAmount = 0;
            Guid? voucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var now = DateTime.UtcNow;
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
                    ? Math.Round(totalAmount * voucher.DiscountValue / 100, 0)
                    : voucher.DiscountValue;

                discountAmount = Math.Min(discountAmount, totalAmount);
                voucherId = voucher.Id;
            }

            decimal finalAmount = totalAmount - discountAmount;

            var order = new Domain.Entities.Order
            {
                Id = Guid.NewGuid(),
                UserId = customerUserId,
                VoucherId = voucherId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                Status = OrderStatusEnum.PENDING.ToString(), 
                CreatedAt = DateTime.UtcNow
            };
            await _orderRepo.AddAsync(order, cancellationToken);

            foreach (var item in request.Items)
            {
                var orderDetail = new Domain.Entities.OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ItemType = "TICKET",
                    ItemId = item.TicketTypeId,
                    Quantity = item.Quantity,
                    UnitPrice = (await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken))!.Price,
                    CreatedAt = DateTime.UtcNow
                };
                await _orderDetailRepo.AddAsync(orderDetail, cancellationToken);
            }

            // Create Payment record
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = finalAmount,
                PaymentMethod = "BANK_TRANSFER",
                TransactionRef = transactionRef,
                Status = PaymentStatusEnum.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(payment, cancellationToken);
            
            if (request.StartDate.HasValue)
            {
                _cache.Set($"OrderStartDate_{order.Id}", request.StartDate.Value, TimeSpan.FromHours(1));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreatePendingOrderResponse
            {
                OrderId = order.Id,
                TransactionRef = transactionRef,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                VoucherCode = request.VoucherCode,
                AccountInfo = accountInfo
            };
        }

        private static string GenerateStrongPassword()
        {
            const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower   = "abcdefghjkmnpqrstuvwxyz";
            const string digits  = "23456789";
            const string special = "@#!%*?&";
            const string all     = upper + lower + digits + special;

            var bytes = new byte[8];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

            var chars = new char[8];
            chars[0] = upper  [bytes[0] % upper.Length];
            chars[1] = lower  [bytes[1] % lower.Length];
            chars[2] = digits [bytes[2] % digits.Length];
            chars[3] = special[bytes[3] % special.Length];
            for (int i = 4; i < 8; i++)
                chars[i] = all[bytes[i] % all.Length];

            var shuffled = chars.OrderBy(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(100)).ToArray();
            return new string(shuffled);
        }
    }
}
