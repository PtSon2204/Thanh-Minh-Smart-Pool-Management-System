using System;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyEntryHistory
{
    public class MyEntryHistoryDto
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public string TicketName { get; set; } = string.Empty;
        public string TicketCategory { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public DateTime? ScanTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? InputMode { get; set; }
        public string? GateId { get; set; }
        public string? Message { get; set; }
    }
}
