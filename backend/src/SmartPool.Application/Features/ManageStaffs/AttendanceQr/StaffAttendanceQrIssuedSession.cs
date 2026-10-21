using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;
public sealed record StaffAttendanceQrIssuedSession(Guid SessionId, long Generation, string QrPayload, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc);
