using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed class StaffAttendanceQrSessionRequest
{
    public Guid ShiftId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Action { get; set; } = string.Empty;
}
