using System;
using System.Collections.Generic;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline
{
    public class CheckoutOnlineResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();
    }
}
