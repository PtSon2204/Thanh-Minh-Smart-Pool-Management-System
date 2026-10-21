using System;

namespace SmartPool.Application.Features.ManagePayments.DTOs
{
    public sealed class PaymentSummaryDto
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;

        public DateTime? PaymentTime { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string ItemsSummary { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? TransactionRef { get; set; }
        public string? StaffName { get; set; }
        public string? CashierName => StaffName;
    }
}
