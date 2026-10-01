using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class Ticket
{
    public Guid Id { get; set; }

    public Guid TicketTypeId { get; set; }

    public Guid? UserId { get; set; }

    public string QrCode { get; set; } = null!;

    public DateTime? IssueDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Status { get; set; }

    public int? RemainingEntries { get; set; }

    public Guid? RowVersion { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<EntryLog> EntryLogs { get; set; } = new List<EntryLog>();

    public virtual TicketType TicketType { get; set; } = null!;

    public virtual User? User { get; set; }
}

