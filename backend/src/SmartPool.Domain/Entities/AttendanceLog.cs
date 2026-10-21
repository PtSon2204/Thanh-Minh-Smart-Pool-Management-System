using System;

namespace SmartPool.Domain.Entities;

public partial class AttendanceLog
{
    public Guid Id { get; set; }

    public Guid EmployeeShiftId { get; set; }

    public string Action { get; set; } = null!;

    public string Method { get; set; } = null!;

    public Guid PerformedByUserId { get; set; }

    public DateTime PerformedAtUtc { get; set; }

    public string? Reason { get; set; }

    public DateTime? BeforeCheckInTime { get; set; }

    public DateTime? BeforeCheckOutTime { get; set; }

    public string? BeforeCheckInMethod { get; set; }

    public string? BeforeCheckOutMethod { get; set; }

    public string? BeforeStatus { get; set; }

    public DateTime? AfterCheckInTime { get; set; }

    public DateTime? AfterCheckOutTime { get; set; }

    public string? AfterCheckInMethod { get; set; }

    public string? AfterCheckOutMethod { get; set; }

    public string? AfterStatus { get; set; }

    public virtual EmployeeShift EmployeeShift { get; set; } = null!;

    public virtual User PerformedByUser { get; set; } = null!;
}
