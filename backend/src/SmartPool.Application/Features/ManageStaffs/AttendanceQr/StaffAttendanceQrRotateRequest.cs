using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed class StaffAttendanceQrRotateRequest
{
    public long Generation { get; set; }
}
