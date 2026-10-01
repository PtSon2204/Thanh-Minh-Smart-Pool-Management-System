using System;
using System.Collections.Generic;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.ConfirmPendingOrder
{
    public class ConfirmPendingOrderResponse
    {
        public bool Success { get; set; }
        public Guid? OrderId { get; set; }
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();
        public string Message { get; set; } = string.Empty;
    }
}
