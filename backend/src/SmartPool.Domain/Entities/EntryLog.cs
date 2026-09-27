using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class EntryLog
{
    public Guid Id { get; set; }

    public Guid? TicketId { get; set; }

    public string? GateId { get; set; }

    public DateTime? ScanTime { get; set; }

    public string Status { get; set; } = null!;

    public string? Message { get; set; }

    public virtual Ticket? Ticket { get; set; }
}
