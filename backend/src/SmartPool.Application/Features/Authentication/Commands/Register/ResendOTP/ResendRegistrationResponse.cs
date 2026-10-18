namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed record ResendRegistrationResponse(
    DateTimeOffset OtpExpiresAtUtc,
    DateTimeOffset RegistrationExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc);
