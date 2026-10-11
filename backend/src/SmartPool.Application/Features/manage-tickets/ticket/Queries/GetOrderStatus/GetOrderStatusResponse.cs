using System.Collections.Generic;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus
{
    public class GetOrderStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();

        /// <summary>Tổng số tiền đã nhận được từ các giao dịch chuyển khoản (dùng cho trường hợp PARTIAL).</summary>
        public decimal PaidAmount { get; set; }

        /// <summary>Số tiền còn thiếu (= FinalAmount - PaidAmount). > 0 khi trạng thái là PARTIAL.</summary>
        public decimal RemainingAmount { get; set; }
    }
}
