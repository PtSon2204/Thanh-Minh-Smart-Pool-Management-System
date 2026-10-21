        using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.Authentication;

internal static class OtpCryptography
{
    public static byte[] ReadKey(string? encoded, string setting)
    {
        byte[] key;
        try { key = Convert.FromBase64String(encoded ?? string.Empty); }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{setting} must be base64.", exception);
        }

        if (key.Length < 32)
            throw new InvalidOperationException($"{setting} must contain at least 32 random bytes.");
        return key;
    }

    public static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Digest(byte[] key, string purpose, Guid id, string value)
    {
        if (id == Guid.Empty || string.IsNullOrEmpty(value))
            throw new ArgumentException("A session ID and nonempty value are required.");
        return Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"{purpose}:{id:N}:{value}")));
    }
}
