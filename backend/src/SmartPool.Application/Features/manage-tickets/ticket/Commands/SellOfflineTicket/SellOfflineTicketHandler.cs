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
                var existingUser = await _userRepo.FirstOrDefaultAsync(u => u.Phone == request.CustomerPhone, cancellationToken);
                if (existingUser != null)
                {
                    customerUserId = existingUser.Id;
                }
                else
                {
                    // Tự động tạo user
                    var password = request.CustomerPhone.Length >= 6 
                        ? request.CustomerPhone.Substring(request.CustomerPhone.Length - 6) 
                        : "123456";
                    
                    var newUser = new Domain.Entities.User
                    {
                        Id = Guid.NewGuid(),
                        Username = request.CustomerPhone,
                        Phone = request.CustomerPhone,
                        PasswordHash = password, 
                        Status = UserStatusEnum.ACTIVE.ToString(),
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

                // Sinh Tickets
                for (int i = 0; i < item.Quantity; i++)
                {
                    DateTime? expiryDate = ticketType.TicketCategory == TicketCategoryEnum.VE_THUONG.ToString()
                        ? DateTime.UtcNow.AddHours(24) // Vé thường 24h
                        : DateTime.UtcNow.AddDays(ticketType.DurationDays ?? 30); // Vé tháng

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = customerUserId,
                        QrCode = $"TKT-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}", // Mã QR
                        IssueDate = DateTime.UtcNow,
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
    }
}
