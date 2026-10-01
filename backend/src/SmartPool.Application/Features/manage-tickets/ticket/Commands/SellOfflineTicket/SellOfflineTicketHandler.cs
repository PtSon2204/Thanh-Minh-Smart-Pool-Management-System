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
        private readonly IUnitOfWork _unitOfWork;

        public SellOfflineTicketHandler(
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.User> userRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IUnitOfWork unitOfWork)
        {
            _ticketRepo = ticketRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _userRepo = userRepo;
            _paymentRepo = paymentRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<SellOfflineTicketResponse> Handle(SellOfflineTicketCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                throw new ArgumentException("Giỏ hàng rỗng.");

            Guid? customerUserId = null;
            AccountInfo? accountInfo = null;

            // Xử lý Khách hàng nếu có SĐT
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

            // Khởi tạo Order
            var order = new Domain.Entities.Order
            {
                Id = Guid.NewGuid(),
                UserId = customerUserId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                Status = OrderStatusEnum.COMPLETED.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            await _orderRepo.AddAsync(order, cancellationToken);

            decimal totalAmount = 0;
            var response = new SellOfflineTicketResponse
            {
                OrderId = order.Id,
                AccountInfo = accountInfo
            };

            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                decimal itemTotal = ticketType.Price * item.Quantity;
                totalAmount += itemTotal;

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

            order.TotalAmount = totalAmount;
            order.FinalAmount = totalAmount;
            
            // Create Payment record for CASH
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = totalAmount,
                PaymentMethod = "CASH",
                Status = PaymentStatusEnum.COMPLETED.ToString(),
                PaymentTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(payment, cancellationToken);

            response.TotalAmount = totalAmount;

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
