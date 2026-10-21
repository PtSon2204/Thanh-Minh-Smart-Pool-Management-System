using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed record StaffAttendanceQrSessionState(DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, long Generation = 1, bool IsStopped = false)
{
    public bool CanRotate(long expectedGeneration, DateTimeOffset now, StaffAttendanceQrSettings settings) =>
        !IsStopped && expectedGeneration == Generation && now >= CreatedAtUtc.AddSeconds(settings.RotationIntervalSeconds) && now < ExpiresAtUtc;
    public bool CanResolve(DateTimeOffset now, StaffAttendanceQrSettings settings) => !IsStopped && now < CreatedAtUtc.AddSeconds(settings.TokenLifetimeSeconds) && now < ExpiresAtUtc;
    public StaffAttendanceQrSessionState Stop() => this with { IsStopped = true };
}
