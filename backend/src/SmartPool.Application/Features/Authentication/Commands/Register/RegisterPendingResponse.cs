namespace SmartPool.Application.Features.Authentication.Commands.Register;

/// <summary>A pending email challenge; no user has been created yet.</summary>
public sealed record RegisterPendingResponse(
    Guid RegistrationId,
    DateTimeOffset OtpExpiresAtUtc,
    DateTimeOffset RegistrationExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc);
