using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder
{
    public class CreatePendingOrderHandler : IRequestHandler<CreatePendingOrderCommand, CreatePendingOrderResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.User> _userRepo;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePendingOrderHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.User> userRepo,
            IUnitOfWork unitOfWork)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
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
                var existingUser = await _userRepo.FirstOrDefaultAsync(u => u.Phone == request.CustomerPhone, cancellationToken);
                if (existingUser != null)
                {
                    customerUserId = existingUser.Id;
                }
                else
                {
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

            // Generate unique TransactionRef (e.g. SP + 6 random alphanumeric)
            var transactionRef = "SP" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            var order = new Domain.Entities.Order
            {
                Id = Guid.NewGuid(),
                UserId = customerUserId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                Status = OrderStatusEnum.PENDING.ToString(), 
                CreatedAt = DateTime.UtcNow
            };
            await _orderRepo.AddAsync(order, cancellationToken);

            decimal totalAmount = 0;

            foreach (var item in request.Items)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(item.TicketTypeId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Loại vé không tồn tại: {item.TicketTypeId}");

                if (ticketType.IsActive != true)
                    throw new InvalidOperationException($"Loại vé {ticketType.Name} đang bị khóa.");

                decimal itemTotal = ticketType.Price * item.Quantity;
                totalAmount += itemTotal;

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
            }

            order.TotalAmount = totalAmount;
            order.FinalAmount = totalAmount;
            
            // Create Payment record
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = totalAmount,
                PaymentMethod = "BANK_TRANSFER",
                TransactionRef = transactionRef,
                Status = PaymentStatusEnum.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(payment, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreatePendingOrderResponse
            {
                OrderId = order.Id,
                TransactionRef = transactionRef,
                TotalAmount = totalAmount,
                AccountInfo = accountInfo
            };
        }
    }
}
