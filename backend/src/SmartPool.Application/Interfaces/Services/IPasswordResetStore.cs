namespace SmartPool.Application.Interfaces.Services;

// UserId null denotes an indistinguishable decoy session. No plaintext secrets enter Redis.
public sealed record PendingPasswordReset(
    Guid RecoveryId, string? Email, Guid? UserId, string? PasswordFingerprint)
{
    public override string ToString() => $"PendingPasswordReset {{ RecoveryId = {RecoveryId}, secrets = [REDACTED] }}";
}

public sealed record PasswordResetIdentity(Guid UserId, string Email, string PasswordFingerprint)
{
    public override string ToString() => "PasswordResetIdentity { secrets = [REDACTED] }";
}

public enum PasswordResetStoreStatus
{
    Success, RateLimited, Unavailable, OtpExpired, InvalidOtp, AttemptsExceeded,
    ResendCooldown, ResendLimitReached, Busy, AlreadyVerified, InvalidToken, TokenExpired
}

public sealed record PasswordResetStoreResult(
    PasswordResetStoreStatus Status,
    DateTimeOffset? OtpExpiresAtUtc = null,
    DateTimeOffset? RecoveryExpiresAtUtc = null,
    DateTimeOffset? ResendAvailableAtUtc = null,
    DateTimeOffset? ResetTokenExpiresAtUtc = null,
    TimeSpan? RetryAfter = null,
    string? LeaseId = null,
    string? Email = null,
    PasswordResetIdentity? Identity = null)
{
    public override string ToString() => $"PasswordResetStoreResult {{ Status = {Status}, secrets = [REDACTED] }}";
}

public interface IPasswordResetStore
{
    Task<PasswordResetStoreResult> TryConsumeRequestQuotaAsync(
        string normalizedEmail, string ipAddress, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryStartAsync(
        PendingPasswordReset request, CancellationToken cancellationToken = default);
    // Activate only after the initial email was sent. A decoy never receives an active code.
    Task<bool> ActivateInitialCodeAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginResendAsync(
        Guid recoveryId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> CompleteResendAsync(
        Guid recoveryId, string leaseId, string otpDigest, CancellationToken cancellationToken = default);
    // Consume the resend attempt and cooldown without replacing the previous OTP.
    // Also used for decoy sessions so public timing remains indistinguishable.
    Task<PasswordResetStoreResult> FinishResendWithoutNewCodeAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<bool> CancelResendAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginVerificationAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> CompleteVerificationAsync(
        Guid recoveryId, string leaseId, string resetTokenDigest, CancellationToken cancellationToken = default);
    Task<bool> ReleaseVerificationAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginCompletionAsync(
        Guid recoveryId, string resetTokenDigest, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseCompletionAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
}
