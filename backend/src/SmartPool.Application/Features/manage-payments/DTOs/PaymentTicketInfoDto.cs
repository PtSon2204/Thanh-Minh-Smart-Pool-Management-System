using System;

namespace SmartPool.Application.Features.ManagePayments.DTOs
{
    public sealed class PaymentTicketInfoDto
    {
        public Guid TicketId { get; set; }

        public string QrCode { get; set; } = string.Empty;

        public string TicketName { get; set; } = string.Empty;

        public string TicketCategory { get; set; } = string.Empty;

        public DateTime? IssueDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public int? RemainingEntries { get; set; }
    }
}
