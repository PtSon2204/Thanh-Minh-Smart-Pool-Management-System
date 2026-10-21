using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed class StaffAttendanceQrSettings
{
    public const string SectionName = "StaffAttendanceQr";
    public int CheckInEarlyMinutes { get; set; } = 20;
    public int TokenLifetimeSeconds { get; set; } = 60;
    public int RotationIntervalSeconds { get; set; } = 60;
    public int SessionLifetimeMinutes { get; set; } = 15;
    public int CheckoutWindowDays { get; set; } = 7;
    public int ManualLookbackDays { get; set; } = 7;

    public bool IsValid() => CheckInEarlyMinutes > 0 && TokenLifetimeSeconds > 0 && RotationIntervalSeconds > 0 &&
        SessionLifetimeMinutes > 0 && CheckoutWindowDays > 0 && ManualLookbackDays > 0;
}
