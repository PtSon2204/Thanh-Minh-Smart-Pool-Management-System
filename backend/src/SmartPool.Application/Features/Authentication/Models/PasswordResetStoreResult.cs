namespace SmartPool.Application.Features.Authentication.Models;

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
