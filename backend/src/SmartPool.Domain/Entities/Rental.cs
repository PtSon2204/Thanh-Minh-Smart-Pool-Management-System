using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class Rental
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public DateTime? RentTime { get; set; }

    public DateTime? ReturnTime { get; set; }

    public decimal? DepositAmount { get; set; }

    public decimal? FeeAmount { get; set; }

    public string? ProductNameSnapshot { get; set; }

    public Guid? SettlementId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public string? Status { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}

