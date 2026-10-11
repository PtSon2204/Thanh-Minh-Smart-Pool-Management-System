using System;

namespace SmartPool.Application.Features.ManagePayments.DTOs
{
    public sealed class PaymentOrderItemDto
    {
        public Guid Id { get; set; }
        public string ItemType { get; set; } = string.Empty;

        public Guid ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string? TicketCategory { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal SubTotal => Quantity * UnitPrice;
    }
}
