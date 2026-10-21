namespace SmartPool.Application.Features.Authentication.Models;

// UserId null denotes an indistinguishable decoy session. No plaintext secrets enter Redis.
public sealed record PendingPasswordReset(
    Guid RecoveryId, string? Email, Guid? UserId, string? PasswordFingerprint)
{
    public override string ToString() => $"PendingPasswordReset {{ RecoveryId = {RecoveryId}, secrets = [REDACTED] }}";
}
