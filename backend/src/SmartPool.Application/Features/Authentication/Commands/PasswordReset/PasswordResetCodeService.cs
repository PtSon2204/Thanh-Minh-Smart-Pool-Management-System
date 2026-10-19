using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

/// <summary>Purpose-bound OTP, reset-token and password-version digests.</summary>
public sealed class PasswordResetCodeService
{
    private readonly byte[] _key;

    public PasswordResetCodeService(IConfiguration configuration)
    {
        _key = OtpCryptography.ReadKey(configuration["PasswordReset:HmacKey"], "PasswordReset:HmacKey");
        var registrationKey = configuration["RegistrationOtp:HmacKey"];
        if (!string.IsNullOrWhiteSpace(registrationKey) &&
            CryptographicOperations.FixedTimeEquals(_key,
                OtpCryptography.ReadKey(registrationKey, "RegistrationOtp:HmacKey")))
            throw new InvalidOperationException("PasswordReset:HmacKey must differ from RegistrationOtp:HmacKey.");
    }

    public string GenerateCode() => OtpCryptography.GenerateCode();

    public string ComputeOtpDigest(Guid recoveryId, string code)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new ArgumentException("A six-digit OTP is required.", nameof(code));
        return OtpCryptography.Digest(_key, "password-reset-otp", recoveryId, code);
    }

    public string GenerateResetToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string ComputeTokenDigest(Guid recoveryId, string token) =>
        OtpCryptography.Digest(_key, "password-reset-token", recoveryId, token);

    public string ComputePasswordFingerprint(Guid userId, string passwordHash) =>
        OtpCryptography.Digest(_key, "password-reset-password-version", userId, passwordHash);
}
