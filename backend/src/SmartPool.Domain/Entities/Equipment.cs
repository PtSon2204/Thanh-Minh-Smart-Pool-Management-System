using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class Equipment
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Metadata { get; set; }

    public string? Location { get; set; }

    public string? Status { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

    public virtual ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
}

