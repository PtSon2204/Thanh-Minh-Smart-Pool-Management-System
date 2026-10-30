namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;

public sealed record RegisterForSwimClassResponse(
    Guid RegistrationId,
    Guid ClassId,
    string RegistrationStatus,
    DateTime RegisteredAt,
    DateTime ConfirmedAt);
