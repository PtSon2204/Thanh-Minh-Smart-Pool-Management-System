using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;
public sealed record StaffAttendanceQrPreviewResponse(string Purpose, Guid SessionId, Guid ShiftId, DateOnly WorkDate,
    StaffAttendanceQrAction Action, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc);
