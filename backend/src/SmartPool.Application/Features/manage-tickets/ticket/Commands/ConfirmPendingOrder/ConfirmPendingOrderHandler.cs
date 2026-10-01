using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderHandler : IRequestHandler<ConfirmPendingOrderCommand, ConfirmPendingOrderResponse>
    {
        private readonly IRepository<Domain.Entities.TicketType> _ticketTypeRepo;
        private readonly IRepository<Domain.Entities.Order> _orderRepo;
        private readonly IRepository<Domain.Entities.OrderDetail> _orderDetailRepo;
        private readonly IRepository<Domain.Entities.Payment> _paymentRepo;
        private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;

        public ConfirmPendingOrderHandler(
            IRepository<Domain.Entities.TicketType> ticketTypeRepo,
            IRepository<Domain.Entities.Order> orderRepo,
            IRepository<Domain.Entities.OrderDetail> orderDetailRepo,
            IRepository<Domain.Entities.Payment> paymentRepo,
            IRepository<Domain.Entities.Ticket> ticketRepo,
            IUnitOfWork unitOfWork,
            IMemoryCache memoryCache)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _orderRepo = orderRepo;
            _orderDetailRepo = orderDetailRepo;
            _paymentRepo = paymentRepo;
            _ticketRepo = ticketRepo;
            _unitOfWork = unitOfWork;
            _memoryCache = memoryCache;
        }

        public async Task<ConfirmPendingOrderResponse> Handle(ConfirmPendingOrderCommand request, CancellationToken cancellationToken)
        {
            var payment = await _paymentRepo.FirstOrDefaultAsync(p => p.TransactionRef == request.TransactionRef, cancellationToken);
            
            if (payment == null)
            {
                return new ConfirmPendingOrderResponse { Success = false, Message = "TransactionRef not found." };
            }

            if (payment.Status == PaymentStatusEnum.COMPLETED.ToString())
            {
                return new ConfirmPendingOrderResponse { Success = true, Message = "Already processed.", OrderId = payment.OrderId };
            }

            //  Kiểm tra số tiền chuyển thực tế có khớp số tiền đơn hàng không
            if (request.ActualAmount < payment.Amount)
            {
                //Nếu khách giả thiếu tiền, cập nhật trạng thái là PARTIAL để thu ngân biết
                payment.Status = PaymentStatusEnum.PARTIAL.ToString();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                return new ConfirmPendingOrderResponse 
                { 
                    Success = false, 
                    Message = $"Amount mismatch. Expected: {payment.Amount}, Received: {request.ActualAmount}" 
                };
            }

            var order = await _orderRepo.GetByIdAsync(payment.OrderId, cancellationToken);
            if (order == null)
            {
                return new ConfirmPendingOrderResponse { Success = false, Message = "Order not found." };
            }

            // Update statuses
            payment.Status = PaymentStatusEnum.COMPLETED.ToString();
            payment.PaymentTime = DateTime.UtcNow;
            
            order.Status = OrderStatusEnum.COMPLETED.ToString();

            var response = new ConfirmPendingOrderResponse
            {
                Success = true,
                OrderId = order.Id,
                Message = "Confirmed and Tickets generated."
            };

            var orderDetails = await _orderDetailRepo.FindAsync(od => od.OrderId == order.Id, cancellationToken);
            
            DateTime issueDate = DateTime.UtcNow;
            if (_memoryCache.TryGetValue($"OrderStartDate_{order.Id}", out DateTime cachedStartDate))
            {
                issueDate = cachedStartDate.ToUniversalTime();
            }

            foreach (var detail in orderDetails)
            {
                var ticketType = await _ticketTypeRepo.GetByIdAsync(detail.ItemId, cancellationToken);
                if (ticketType == null) continue;

                for (int i = 0; i < detail.Quantity; i++)
                {
                    // VE_LUOT online: hết hạn cuối ngày phát hành (23:59:59 UTC)
                    // VE_THANG: hết hạn sau DurationDays tính từ ngày bắt đầu
                    DateTime expiryDate = ticketType.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString()
                        ? issueDate.Date.AddDays(1).AddTicks(-1)
                        : issueDate.AddDays(ticketType.DurationDays ?? 30);

                    var ticket = new Domain.Entities.Ticket
                    {
                        Id = Guid.NewGuid(),
                        TicketTypeId = ticketType.Id,
                        UserId = order.UserId,
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
                        TicketCategory = ticketType.TicketCategory ?? ""
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Store in cache for 10 minutes so frontend can fetch it
            _memoryCache.Set($"OrderTickets_{order.Id}", response.Tickets, TimeSpan.FromMinutes(10));

            return response;
        }
    }
}
