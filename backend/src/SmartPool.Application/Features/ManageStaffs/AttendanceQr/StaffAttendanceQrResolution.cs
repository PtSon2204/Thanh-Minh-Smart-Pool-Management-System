using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;
public sealed record StaffAttendanceQrResolution(Guid AssignmentId, Guid SessionId, Guid ShiftId, DateOnly WorkDate,
    StaffAttendanceQrAction Action, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc)
{
    public bool CheckIn => Action == StaffAttendanceQrAction.CheckIn;
}
