using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? TransactionRef { get; set; }

    public string? Status { get; set; }

    public Guid? CashierId { get; set; }

    public DateTime? PaymentTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Employee? Cashier { get; set; }

    public virtual Order Order { get; set; } = null!;
}
