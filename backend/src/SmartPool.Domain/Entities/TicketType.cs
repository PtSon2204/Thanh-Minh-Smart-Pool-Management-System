using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class TicketType
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string TicketCategory { get; set; } = null!;

    public decimal Price { get; set; }

    public int? DurationDays { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
