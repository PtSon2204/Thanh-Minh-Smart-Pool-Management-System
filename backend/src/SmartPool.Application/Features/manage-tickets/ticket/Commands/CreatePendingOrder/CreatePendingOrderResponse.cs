using System;
using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CreatePendingOrder
{
    public class CreatePendingOrderResponse
    {
        public Guid OrderId { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public AccountInfo? AccountInfo { get; set; }
    }
}
