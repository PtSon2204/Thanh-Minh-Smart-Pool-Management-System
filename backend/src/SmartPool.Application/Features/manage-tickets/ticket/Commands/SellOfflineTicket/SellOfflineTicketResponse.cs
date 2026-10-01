using System;
using System.Collections.Generic;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.SellOfflineTicket
{
    public class SellOfflineTicketResponse
    {
        public Guid OrderId { get; set; }
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();
        public decimal TotalAmount { get; set; }
        public AccountInfo? AccountInfo { get; set; }
    }
}
