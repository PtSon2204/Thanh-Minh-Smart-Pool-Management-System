namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrations;

public sealed record ClassRegistrationResponse(
    Guid RegistrationId,
    Guid CustomerUserId,
    string CustomerName,
    string Status,
    DateTime RegisteredAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    Guid? CancelledByUserId,
    string? CancelReason,
    DateTime UpdatedAt);
