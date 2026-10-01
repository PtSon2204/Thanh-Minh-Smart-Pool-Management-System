using System;

namespace SmartPool.Application.Features.ManageTickets.Ticket.DTOs
{
    public class SellOfflineTicketItem
    {
        public Guid TicketTypeId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
