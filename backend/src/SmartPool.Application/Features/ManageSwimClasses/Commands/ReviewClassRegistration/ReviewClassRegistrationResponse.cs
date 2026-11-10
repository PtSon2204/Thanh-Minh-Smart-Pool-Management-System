namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

public sealed record ReviewClassRegistrationResponse(
    Guid RegistrationId,
    Guid ClassId,
    string Status,
    Guid ReviewedByUserId,
    DateTime ReviewedAt,
    string? ReviewNote,
    DateTime? ConfirmedAt,
    DateTime UpdatedAt);
