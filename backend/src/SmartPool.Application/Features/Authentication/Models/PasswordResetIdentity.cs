namespace SmartPool.Application.Features.Authentication.Models;

public sealed record PasswordResetIdentity(Guid UserId, string Email, string PasswordFingerprint)
{
    public override string ToString() => "PasswordResetIdentity { secrets = [REDACTED] }";
}
