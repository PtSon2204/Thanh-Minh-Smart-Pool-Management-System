using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public static class StaffAttendanceQrAssignmentPolicy
{
    public static bool SupportsAction(SmartPool.Domain.Entities.EmployeeShift assignment, StaffAttendanceQrAction action) => action == StaffAttendanceQrAction.CheckIn
        ? assignment.Status == "Scheduled" && assignment.CheckInTime is null
        : assignment.Status == "Present" && assignment.CheckInTime is not null && assignment.CheckOutTime is null;
}
