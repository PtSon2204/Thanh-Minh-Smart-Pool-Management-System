using Microsoft.Extensions.Configuration;

namespace SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;

/// <summary>Generates registration codes and their keyed, session-bound digests.</summary>
public sealed class RegistrationOtpCodeService
{
    private readonly byte[] _key;

    public RegistrationOtpCodeService(IConfiguration configuration)
    {
        _key = OtpCryptography.ReadKey(configuration["RegistrationOtp:HmacKey"], "RegistrationOtp:HmacKey");
    }

    public string GenerateCode() => OtpCryptography.GenerateCode();

    public string ComputeDigest(Guid registrationId, string code)
    {
        if (registrationId == Guid.Empty || code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new ArgumentException("A registration ID and six-digit OTP are required.");

        return OtpCryptography.Digest(_key, "registration-otp", registrationId, code);
    }
}
