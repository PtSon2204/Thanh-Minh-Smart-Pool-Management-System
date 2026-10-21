using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

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
