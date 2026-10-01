using System;

namespace SmartPool.Application.Features.ManageTickets.Ticket.DTOs
{
    public class TicketInfo
    {
        public Guid Id { get; set; }
        public string QrCode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public string TicketCategory { get; set; } = string.Empty;
    }
}
