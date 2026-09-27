using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class InventoryLog
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ChangeType { get; set; } = null!;

    public int Quantity { get; set; }

    public string? Note { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Product Product { get; set; } = null!;
}
