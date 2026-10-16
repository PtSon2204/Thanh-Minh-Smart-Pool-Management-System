namespace SmartPool.Application.Interfaces.Services;

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

public sealed record VerifiedRegistration(
    string Username,
    string Email,
    string Phone,
    string PasswordHash,
    string FullName,
    DateOnly? DateOfBirth,
    string? Address)
{
    public override string ToString() => "VerifiedRegistration { secrets = [REDACTED] }";
}

public enum RegistrationOtpStoreStatus
{
    Success,
    PendingRegistration,
    RateLimited,
    Unavailable,
    OtpExpired,
    InvalidOtp,
    AttemptsExceeded,
    ResendCooldown,
    ResendLimitReached,
    Busy,
    AlreadyVerified
}

public sealed record RegistrationOtpStoreResult(
    RegistrationOtpStoreStatus Status,
    DateTimeOffset? OtpExpiresAtUtc = null,
    DateTimeOffset? RegistrationExpiresAtUtc = null,
    DateTimeOffset? ResendAvailableAtUtc = null,
    TimeSpan? RetryAfter = null,
    string? LeaseId = null,
    string? Email = null,
    VerifiedRegistration? Registration = null)
{
    public override string ToString() => $"RegistrationOtpStoreResult {{ Status = {Status}, secrets = [REDACTED] }}";
}

public interface IRegistrationOtpStore
{
    // Counts every valid registration attempt before database lookups or password hashing.
    Task<RegistrationOtpStoreResult> TryConsumeStartQuotaAsync(
        string email, string ipAddress, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> TryStartAsync(
        PendingRegistration registration, string ipAddress, CancellationToken cancellationToken = default);

    // Only cancels the matching pending session. Rate counters are intentionally retained.
    Task<bool> CancelStartAsync(Guid registrationId, string email, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> TryBeginVerificationAsync(
        Guid registrationId, string otpDigest, CancellationToken cancellationToken = default);

    Task<bool> CompleteVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);

    Task<bool> ReleaseVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);

    // Keep the old OTP active until CompleteResendAsync succeeds.
    Task<RegistrationOtpStoreResult> TryBeginResendAsync(
        Guid registrationId, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> CompleteResendAsync(
        Guid registrationId, string leaseId, string newOtpDigest, CancellationToken cancellationToken = default);

    Task<bool> CancelResendAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);
}
