using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class MaintenanceLog
{
    public Guid Id { get; set; }

    public Guid EquipmentId { get; set; }

    public Guid? ScheduleId { get; set; }

    public DateTime ActualDate { get; set; }

    public decimal? Cost { get; set; }

    public Guid? PerformedBy { get; set; }

    public string? VendorName { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Equipment Equipment { get; set; } = null!;

    public virtual User? PerformedByNavigation { get; set; }

    public virtual MaintenanceSchedule? Schedule { get; set; }
}
