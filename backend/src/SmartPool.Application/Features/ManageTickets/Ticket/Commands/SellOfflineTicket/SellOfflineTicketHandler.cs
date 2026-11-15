using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket
{
    public class SellOfflineTicketHandler : IRequestHandler<SellOfflineTicketCommand, SellOfflineTicketResponse>
    {
        private readonly ITicketOrderOperations _ticketOrderOperations;
        private readonly ITicketTypeOperations _ticketTypeOperations;
        private readonly IUserOperations _userOperations;
        private readonly IVoucherOperations _voucherOperations;
        private readonly TimeProvider _timeProvider;

        public SellOfflineTicketHandler(
            ITicketOrderOperations ticketOrderOperations,
            ITicketTypeOperations ticketTypeOperations,
            IUserOperations userOperations,
            IVoucherOperations voucherOperations,
            TimeProvider timeProvider)
        {
            _ticketOrderOperations = ticketOrderOperations;
            _ticketTypeOperations = ticketTypeOperations;
            _userOperations = userOperations;
            _voucherOperations = voucherOperations;
            _timeProvider = timeProvider;
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
                var existingUser = await _userOperations.GetByPhoneOrUsernameAsync(
                    request.CustomerPhone,
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
                    await _userOperations.AddUserAsync(newUser, cancellationToken);
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
                var ticketType = await _ticketTypeOperations.GetByIdAsync(item.TicketTypeId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                ticketTypesCache[item.TicketTypeId] = ticketType;
                int entries = (item.RequestedEntries.HasValue && item.RequestedEntries.Value > 0) ? item.RequestedEntries.Value : 1;
                bool isVeLuot = ticketType.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString();
                int priceMultiplier = isVeLuot ? entries : 1;
                totalAmount += ticketType.Price * priceMultiplier * item.Quantity;
            }

            // 3. Validate và áp dụng Voucher (nếu có)
            decimal discountAmount = 0;
            Guid? voucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var voucher = await _voucherOperations.GetUsableVoucherByCodeAsync(
                    request.VoucherCode,
                    now,
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

            var response = new SellOfflineTicketResponse
            {
                OrderId = order.Id,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                VoucherCode = request.VoucherCode,
                AccountInfo = accountInfo
            };

            DateTime issueDate = DateTime.UtcNow;
            var orderDetails = new List<OrderDetail>();
            var tickets = new List<Domain.Entities.Ticket>();

            // 5. Tạo OrderDetail và Ticket
            foreach (var item in request.Items)
            {
                var ticketType = ticketTypesCache[item.TicketTypeId];
                int entries = (item.RequestedEntries.HasValue && item.RequestedEntries.Value > 0) ? item.RequestedEntries.Value : 1;
                bool isVeLuot = ticketType.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString();

                var orderDetail = new Domain.Entities.OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ItemType = "TICKET",
                    ItemId = ticketType.Id,
                    Quantity = item.Quantity,
                    UnitPrice = isVeLuot ? ticketType.Price * entries : ticketType.Price,
                    CustomEntries = isVeLuot ? entries : null,
                    CreatedAt = DateTime.UtcNow
                };
                orderDetails.Add(orderDetail);

                // VE_LUOT 1 lượt tại quầy: không tạo Ticket entity (không có QR)
                if (isVeLuot && entries <= 1)
                    continue;

                for (int i = 0; i < item.Quantity; i++)
                {
                    int? remainingEntries = isVeLuot ? entries : null;
                    DateTime? expiryDate = TicketValidityPolicy.GetExpiryDate(issueDate, ticketType.TicketCategory, entries);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = customerUserId,
                        QrCode = await GenerateUniqueCodeAsync(cancellationToken),
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        RemainingEntries = remainingEntries,
                        Status = TicketStatusEnum.ACTIVE.ToString(),
                        CreatedAt = DateTime.UtcNow
                    };

                    tickets.Add(ticket);

                    response.Tickets.Add(new TicketInfo
                    {
                        Id = ticket.Id,
                        QrCode = ticket.QrCode,
                        ExpiryDate = ticket.ExpiryDate,
                        TicketCategory = ticketType.TicketCategory,
                        RemainingEntries = ticket.RemainingEntries
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

            await _ticketOrderOperations.CreateOfflineSaleAsync(order, orderDetails, payment, tickets, cancellationToken);

            return response;
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
