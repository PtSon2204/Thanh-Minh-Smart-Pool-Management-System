using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class EmployeeShift
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid ShiftId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string? Status { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public string? CheckInMethod { get; set; }

    public string? CheckOutMethod { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;

    public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
}

