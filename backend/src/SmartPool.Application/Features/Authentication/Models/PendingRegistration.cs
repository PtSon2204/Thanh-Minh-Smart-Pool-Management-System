namespace SmartPool.Application.Features.Authentication.Models;

// PasswordHash and OtpDigest are already protected before reaching this store.
// Neither the plaintext password nor the plaintext OTP belongs in Redis.
public sealed record PendingRegistration(
    Guid RegistrationId,
    string Username,
    string Email,
    string Phone,
    string PasswordHash,
    string FullName,
    DateOnly? DateOfBirth,
    string? Address,
    string OtpDigest)
{
    public override string ToString() => $"PendingRegistration {{ RegistrationId = {RegistrationId}, secrets = [REDACTED] }}";
}
