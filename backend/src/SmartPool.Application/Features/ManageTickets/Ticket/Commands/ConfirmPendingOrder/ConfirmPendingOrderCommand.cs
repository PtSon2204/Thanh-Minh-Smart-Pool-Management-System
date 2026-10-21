using MediatR;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;
    
namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderCommand : IRequest<ConfirmPendingOrderResponse>
    {
        /// <summary>Mã SP... được tạo khi khách đặt hàng, dùng để tìm đơn hàng.</summary>
        public string TransactionRef { get; set; } = string.Empty;

        /// <summary>Số tiền ngân hàng thực chuyển (từ SePay).</summary>
        public decimal ActualAmount { get; set; }

        /// <summary>Mã giao dịch ngân hàng gốc (referenceCode từ SePay). Dùng làm Idempotency Key.</summary>
        public string? BankReferenceCode { get; set; }
    }
}
