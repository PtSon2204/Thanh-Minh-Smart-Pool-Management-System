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
        public string Status { get; set; } = "PENDING";
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime ServerTimeUtc { get; set; }
        public List<OnlineOrderItemDto> Items { get; set; } = [];
        public List<TicketInfo> Tickets { get; set; } = new List<TicketInfo>();
    }
}
