using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff
{

public static class StaffAttendanceTiming
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static bool CanCheckIn(EmployeeShift assignment, DateTime utcNow, int checkInEarlyMinutes)
    {
        var (scheduledStart, scheduledEnd) = Interval(assignment.WorkDate, assignment.Shift);
        return utcNow >= scheduledStart.AddMinutes(-checkInEarlyMinutes) && utcNow < scheduledEnd;
    }

    public static bool CanCheckOut(EmployeeShift assignment, DateTime utcNow)
    {
        var (_, scheduledEnd) = Interval(assignment.WorkDate, assignment.Shift);
        return utcNow >= scheduledEnd && assignment.CheckInTime is not null && assignment.CheckInTime < utcNow;
    }

    public static (DateTime Start, DateTime End) Interval(DateOnly date, Shift shift)
    {
        var start = date.ToDateTime(shift.StartTime);
        var end = date.ToDateTime(shift.EndTime).AddDays(shift.EndTime < shift.StartTime ? 1 : 0);
        return (TimeZoneInfo.ConvertTimeToUtc(start, VietnamTimeZone), TimeZoneInfo.ConvertTimeToUtc(end, VietnamTimeZone));
    }
}
}
