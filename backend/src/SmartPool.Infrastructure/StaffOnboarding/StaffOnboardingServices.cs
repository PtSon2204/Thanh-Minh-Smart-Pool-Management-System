using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.StaffOnboarding;

public sealed class StaffOnboardingOptions
{
    public string EncryptionKey { get; set; } = "";
    public string IdentityKey { get; set; } = "";
    public string FrontendOrigin { get; set; } = "";
}

public sealed class StaffOnboardingServices(
    IOptions<StaffOnboardingOptions> options, IPasswordHasher passwords,
    IEmailService email, IStaffDocumentStorage storage, StaffDocumentCleanup cleanup)
{
    public StaffOnboardingOptions Options => options.Value;
    public IPasswordHasher Passwords => passwords;
    public IEmailService Email => email;
    public IStaffDocumentStorage Storage => storage;
    public StaffDocumentCleanup Cleanup => cleanup;

    private static byte[] Key(string encoded)
    {
        try
        {
            var key = Convert.FromBase64String(encoded);
            if (key.Length == 32) return key;
        }
        catch (FormatException) { }
        throw new StaffOnboardingException("ONBOARDING_KEYS_UNAVAILABLE", 503);
    }

    public string Protect(string value, string purpose)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var cipher = new AesGcm(Key(Options.EncryptionKey), 16);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(purpose));
        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }

    public string Unprotect(string value, string purpose)
    {
        var bytes = Convert.FromBase64String(value);
        var plaintext = new byte[bytes.Length - 28];
        using var cipher = new AesGcm(Key(Options.EncryptionKey), 16);
        cipher.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plaintext,
            Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(plaintext);
    }

    public string ProtectProfile(StaffLegalProfile profile) => Protect(JsonSerializer.Serialize(profile), "staff-legal-v1");
    public StaffLegalProfile Profile(string value) => JsonSerializer.Deserialize<StaffLegalProfile>(Unprotect(value, "staff-legal-v1"))!;
    public string IdentityDigest(string value) => Convert.ToHexString(HMACSHA256.HashData(Key(Options.IdentityKey), Encoding.UTF8.GetBytes("staff-cccd-v1:" + value)));
    public static string TokenDigest(Guid invitationId, string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"staff-invitation-v1:{invitationId:D}:{token}")));

    public string InvitationLink(Guid invitationId, string token)
    {
        return $"{FrontendOrigin()}/staff/onboarding#invitation={invitationId:D}&token={token}";
    }

    public string StaffScheduleLink() => $"{FrontendOrigin()}/admin/staff/schedule";

    private string FrontendOrigin()
    {
        if (!Uri.TryCreate(Options.FrontendOrigin, UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(origin.Scheme == "http" && origin.IsLoopback)) ||
            origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.AbsolutePath != "/")
            throw new StaffOnboardingException("ONBOARDING_ORIGIN_UNAVAILABLE", 503);
        return origin.GetLeftPart(UriPartial.Authority);
    }
}
