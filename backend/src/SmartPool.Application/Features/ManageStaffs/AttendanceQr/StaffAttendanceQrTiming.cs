using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public static class StaffAttendanceQrTiming
{
    public static bool CanIssueCheckIn(DateTime utcNow, DateTime scheduledStartUtc, DateTime scheduledEndUtc, StaffAttendanceQrSettings settings) =>
        utcNow < scheduledEndUtc && utcNow >= scheduledStartUtc.AddMinutes(-settings.CheckInEarlyMinutes);
    public static DateTimeOffset CapTokenExpiry(DateTimeOffset now, DateTimeOffset sessionExpiresAt, DateTimeOffset actionWindowEndsAt, StaffAttendanceQrSettings settings) =>
        new[] { now.AddSeconds(settings.TokenLifetimeSeconds), sessionExpiresAt, actionWindowEndsAt }.Min();
    public static DateTimeOffset CapSessionExpiry(DateTimeOffset now, DateTimeOffset actionWindowEndsAt, StaffAttendanceQrSettings settings) =>
        new[] { now.AddMinutes(settings.SessionLifetimeMinutes), actionWindowEndsAt }.Min();
}
