using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManagePayments.DTOs
{
    public sealed class PaymentDetailDto
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? TransactionRef { get; set; }
        public DateTime? PaymentTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public DateTime? OrderCreatedAt { get; set; }
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerAddress { get; set; }
        public Guid? StaffId { get; set; }
        public string? StaffName { get; set; }
        public string? StaffPhone { get; set; }
        public string? StaffEmail { get; set; }

        public string? VoucherCode { get; set; }
        public string? VoucherDiscountType { get; set; }
        public decimal? VoucherDiscountValue { get; set; }

        public List<PaymentOrderItemDto> Items { get; set; } = new List<PaymentOrderItemDto>();

        public List<PaymentTicketInfoDto> Tickets { get; set; } = new List<PaymentTicketInfoDto>();
    }
}
