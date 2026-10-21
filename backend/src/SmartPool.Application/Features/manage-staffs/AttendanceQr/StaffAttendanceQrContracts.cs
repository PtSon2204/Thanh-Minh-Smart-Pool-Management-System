using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public enum StaffAttendanceQrAction
{
    CheckIn,
    CheckOut
}

public sealed class StaffAttendanceQrSessionRequest
{
    public Guid ShiftId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Action { get; set; } = string.Empty;
}

public sealed class StaffAttendanceQrPayloadRequest
{
    public string QrPayload { get; set; } = string.Empty;
}

public sealed class StaffAttendanceQrRotateRequest
{
    public long Generation { get; set; }
}

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

public sealed record StaffAttendanceQrIssueRequest(Guid ShiftId, DateOnly WorkDate, StaffAttendanceQrAction Action);
public sealed record StaffAttendanceQrIssuedSession(Guid SessionId, long Generation, string QrPayload, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc);
public sealed record StaffAttendanceQrResolution(Guid AssignmentId, Guid SessionId, Guid ShiftId, DateOnly WorkDate,
    StaffAttendanceQrAction Action, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc)
{
    public bool CheckIn => Action == StaffAttendanceQrAction.CheckIn;
}
public sealed record StaffAttendanceQrPreviewResponse(string Purpose, Guid SessionId, Guid ShiftId, DateOnly WorkDate,
    StaffAttendanceQrAction Action, DateTime TokenExpiresAtUtc, DateTime SessionExpiresAtUtc);

public static class StaffAttendanceQrToken
{
    public const string Prefix = "SMARTPOOL:STAFF:V1:";

    public static string Create(Guid sessionId)
    {
        var random = RandomNumberGenerator.GetBytes(32);
        return Prefix + sessionId.ToString("N") + "." + Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryParse(string? value, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var parts = value[Prefix.Length..].Split('.', StringSplitOptions.None);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out _) || parts[1].Length != 43 ||
            parts[1].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            return false;

        token = value;
        return true;
    }

    public static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool TryGetSessionId(string token, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        return TryParse(token, out _) && Guid.TryParseExact(token[Prefix.Length..].Split('.')[0], "N", out sessionId);
    }

    public static bool TryParseAction(string? value, out StaffAttendanceQrAction action)
    {
        action = default;
        if (string.Equals(value, nameof(StaffAttendanceQrAction.CheckIn), StringComparison.OrdinalIgnoreCase))
        {
            action = StaffAttendanceQrAction.CheckIn;
            return true;
        }
        if (string.Equals(value, nameof(StaffAttendanceQrAction.CheckOut), StringComparison.OrdinalIgnoreCase))
        {
            action = StaffAttendanceQrAction.CheckOut;
            return true;
        }
        return false;
    }
}

public static class StaffAttendanceQrTiming
{
    public static bool CanIssueCheckIn(DateTime utcNow, DateTime scheduledStartUtc, DateTime scheduledEndUtc, StaffAttendanceQrSettings settings) =>
        utcNow < scheduledEndUtc && utcNow >= scheduledStartUtc.AddMinutes(-settings.CheckInEarlyMinutes);
    public static DateTimeOffset CapTokenExpiry(DateTimeOffset now, DateTimeOffset sessionExpiresAt, DateTimeOffset actionWindowEndsAt, StaffAttendanceQrSettings settings) =>
        new[] { now.AddSeconds(settings.TokenLifetimeSeconds), sessionExpiresAt, actionWindowEndsAt }.Min();
    public static DateTimeOffset CapSessionExpiry(DateTimeOffset now, DateTimeOffset actionWindowEndsAt, StaffAttendanceQrSettings settings) =>
        new[] { now.AddMinutes(settings.SessionLifetimeMinutes), actionWindowEndsAt }.Min();
}

public static class StaffAttendanceQrAssignmentPolicy
{
    public static bool SupportsAction(SmartPool.Domain.Entities.EmployeeShift assignment, StaffAttendanceQrAction action) => action == StaffAttendanceQrAction.CheckIn
        ? assignment.Status == "Scheduled" && assignment.CheckInTime is null
        : assignment.Status == "Present" && assignment.CheckInTime is not null && assignment.CheckOutTime is null;
}

public sealed record StaffAttendanceQrSessionState(DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, long Generation = 1, bool IsStopped = false)
{
    public bool CanRotate(long expectedGeneration, DateTimeOffset now, StaffAttendanceQrSettings settings) =>
        !IsStopped && expectedGeneration == Generation && now >= CreatedAtUtc.AddSeconds(settings.RotationIntervalSeconds) && now < ExpiresAtUtc;
    public bool CanResolve(DateTimeOffset now, StaffAttendanceQrSettings settings) => !IsStopped && now < CreatedAtUtc.AddSeconds(settings.TokenLifetimeSeconds) && now < ExpiresAtUtc;
    public StaffAttendanceQrSessionState Stop() => this with { IsStopped = true };
}

public sealed class StaffAttendanceQrConflictException(string message) : Exception(message);
public sealed class StaffAttendanceQrForbiddenException : Exception;
