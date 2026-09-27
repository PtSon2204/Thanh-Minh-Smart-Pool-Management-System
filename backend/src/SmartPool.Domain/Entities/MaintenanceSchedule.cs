using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class MaintenanceSchedule
{
    public Guid Id { get; set; }

    public Guid EquipmentId { get; set; }

    public Guid? AssignedTo { get; set; }

    public DateOnly PlannedDate { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual Equipment Equipment { get; set; } = null!;

    public virtual ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();
}
