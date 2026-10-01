using System.Collections.Generic;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetOrderStatus
{
    public class GetOrderStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();
    }
}
