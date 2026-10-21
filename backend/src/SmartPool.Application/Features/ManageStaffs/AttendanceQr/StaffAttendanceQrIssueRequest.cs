using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed record StaffAttendanceQrIssueRequest(Guid ShiftId, DateOnly WorkDate, StaffAttendanceQrAction Action);
