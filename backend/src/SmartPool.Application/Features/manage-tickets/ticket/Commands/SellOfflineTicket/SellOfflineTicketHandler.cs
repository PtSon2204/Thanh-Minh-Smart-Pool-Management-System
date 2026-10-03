using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket
{
    public class SellOfflineTicketHandler : IRequestHandler<SellOfflineTicketCommand, SellOfflineTicketResponse>
    {
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.User> _userRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Voucher> _voucherRepo;
        private readonly IUnitOfWork _unitOfWork;

        public SellOfflineTicketHandler(
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.User> userRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Voucher> voucherRepo,
            IUnitOfWork unitOfWork)
        {
            _ticketRepo = ticketRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _userRepo = userRepo;
            _paymentRepo = paymentRepo;
            _voucherRepo = voucherRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<SellOfflineTicketResponse> Handle(SellOfflineTicketCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                throw new ArgumentException("Giỏ hàng rỗng.");

            Guid? customerUserId = null;
            AccountInfo? accountInfo = null;

            // 1. Xử lý Khách hàng nếu có SĐT
            if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
            {
                // Tìm theo Phone HOẶC Username (SĐT) để tránh UNIQUE constraint
                var existingUser = await _userRepo.FirstOrDefaultAsync(
                    u => u.Phone == request.CustomerPhone || u.Username == request.CustomerPhone,
                    cancellationToken);

                if (existingUser != null)
                {
                    customerUserId = existingUser.Id;
                }
                else
                {
                    // Username = SĐT (hệ thống đăng nhập bằng SĐT)
                    // Mật khẩu ngẫu nhiên đủ mạnh: HOA + thường + số + ký tự đặc biệt, 8 ký tự
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

            // 2. Tính tổng tiền trước giảm giá
            decimal totalAmount = 0;
            var ticketTypesCache = new Dictionary<Guid, Domain.Entities.TicketType>();

            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                ticketTypesCache[item.TicketTypeId] = ticketType;
                totalAmount += ticketType.Price * item.Quantity;
            }

            // 3. Validate và áp dụng Voucher (nếu có)
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

                // Kiểm tra điều kiện tổng đơn tối thiểu
                if (voucher.MinOrderValue.HasValue && totalAmount < voucher.MinOrderValue.Value)
                    throw new InvalidOperationException(
                        $"Đơn hàng tối thiểu {voucher.MinOrderValue:N0}đ để dùng mã này (hiện tại: {totalAmount:N0}đ).");

                // Tính giảm giá
                discountAmount = voucher.DiscountType == "PERCENTAGE"
                    ? Math.Round(totalAmount * voucher.DiscountValue / 100, 0)
                    : voucher.DiscountValue;

                // Không được giảm nhiều hơn tổng tiền
                discountAmount = Math.Min(discountAmount, totalAmount);
                voucherId = voucher.Id;
            }

            decimal finalAmount = totalAmount - discountAmount;

            // 4. Tạo Order
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
                Status = OrderStatusEnum.COMPLETED.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            await _orderRepo.AddAsync(order, cancellationToken);

            var response = new SellOfflineTicketResponse
            {
                OrderId = order.Id,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                VoucherCode = request.VoucherCode,
                AccountInfo = accountInfo
            };

            // 5. Tạo OrderDetail và Ticket
            foreach (var item in request.Items)
            {
                var ticketType = ticketTypesCache[item.TicketTypeId];
                decimal itemTotal = ticketType.Price * item.Quantity;

                // Tạo Order Detail
                var orderDetail = new Domain.Entities.OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ItemType = "TICKET",
                    ItemId = ticketType.Id,
                    Quantity = item.Quantity,
                    UnitPrice = ticketType.Price,
                    CreatedAt = DateTime.UtcNow
                };
                await _orderDetailRepo.AddAsync(orderDetail, cancellationToken);

                // VE_LUOT tại quầy: không tạo Ticket entity (không có QR)
                if (ticketType.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString())
                    continue;

                for (int i = 0; i < item.Quantity; i++)
                {
                    DateTime issueDate = (request.StartDate ?? DateTime.UtcNow).ToUniversalTime();
                    DateTime expiryDate = issueDate.AddDays(ticketType.DurationDays ?? 30);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = customerUserId,
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
                        TicketCategory = ticketType.TicketCategory
                    });
                }
            }

            // 6. Tạo Payment record (CASH - thu tiền FinalAmount sau khi đã trừ voucher)
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = finalAmount,
                PaymentMethod = "CASH",
                Status = PaymentStatusEnum.COMPLETED.ToString(),
                PaymentTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(payment, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return response;
        }

        /// <summary>Sinh mật khẩu ngẫu nhiên đủ mạnh: 8 ký tự, có HOA + thường + số + đặc biệt.</summary>
        private static string GenerateStrongPassword()
        {
            const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower   = "abcdefghjkmnpqrstuvwxyz";
            const string digits  = "23456789";
            const string special = "@#!%*?&";
            const string all     = upper + lower + digits + special;

            var bytes = new byte[8];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

            // Đảm bảo mỗi nhóm có ít nhất 1 ký tự
            var chars = new char[8];
            chars[0] = upper  [bytes[0] % upper.Length];
            chars[1] = lower  [bytes[1] % lower.Length];
            chars[2] = digits [bytes[2] % digits.Length];
            chars[3] = special[bytes[3] % special.Length];
            for (int i = 4; i < 8; i++)
                chars[i] = all[bytes[i] % all.Length];

            // Xáo trộn để tránh pattern cố định (HOA luôn ở đầu)
            var shuffled = chars.OrderBy(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(100)).ToArray();
            return new string(shuffled);
        }
    }
}
