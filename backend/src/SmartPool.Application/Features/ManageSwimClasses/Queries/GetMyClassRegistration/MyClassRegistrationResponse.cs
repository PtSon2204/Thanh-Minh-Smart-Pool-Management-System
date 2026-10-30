namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;

public sealed record MyClassRegistrationSnapshot(
    Guid Id,
    Guid ClassId,
    Guid CustomerUserId,
    string Status,
    DateTime RegisteredAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt);

public sealed record MyClassRegistrationResponse(
    Guid RegistrationId,
    Guid ClassId,
    string RegistrationStatus,
    DateTime RegisteredAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt);
