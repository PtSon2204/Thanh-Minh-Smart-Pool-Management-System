using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class EmployeeShift
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid ShiftId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string? Status { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;
}
