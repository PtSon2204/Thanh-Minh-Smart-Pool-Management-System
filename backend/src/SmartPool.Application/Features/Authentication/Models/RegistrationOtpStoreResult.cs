namespace SmartPool.Application.Features.Authentication.Models;

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
