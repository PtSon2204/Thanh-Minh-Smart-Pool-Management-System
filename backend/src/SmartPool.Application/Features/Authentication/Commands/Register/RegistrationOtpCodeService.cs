using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

/// <summary>Generates registration codes and their keyed, session-bound digests.</summary>
public sealed class RegistrationOtpCodeService
{
    private readonly byte[] _key;

    public RegistrationOtpCodeService(IConfiguration configuration)
    {
        try
        {
            _key = Convert.FromBase64String(configuration["RegistrationOtp:HmacKey"] ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("RegistrationOtp:HmacKey must be base64.", exception);
        }

        if (_key.Length < 32)
            throw new InvalidOperationException("RegistrationOtp:HmacKey must contain at least 32 random bytes.");
    }

    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string ComputeDigest(Guid registrationId, string code)
    {
        if (registrationId == Guid.Empty || code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new ArgumentException("A registration ID and six-digit OTP are required.");

        var bytes = Encoding.UTF8.GetBytes($"registration-otp:{registrationId:N}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(_key, bytes));
    }
}
