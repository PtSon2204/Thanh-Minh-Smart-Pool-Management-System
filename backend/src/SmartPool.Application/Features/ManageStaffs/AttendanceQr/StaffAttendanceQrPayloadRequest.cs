using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed class StaffAttendanceQrPayloadRequest
{
    public string QrPayload { get; set; } = string.Empty;
}
