namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;

public sealed record MySwimClassItem(
    Guid RegistrationId,
    Guid ClassId,
    string ClassName,
    string ClassStatus,
    string? CoachName,
    int SessionCount,
    DateTime? NextSessionAt,
    string RegistrationStatus,
    DateTime RegisteredAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt);
