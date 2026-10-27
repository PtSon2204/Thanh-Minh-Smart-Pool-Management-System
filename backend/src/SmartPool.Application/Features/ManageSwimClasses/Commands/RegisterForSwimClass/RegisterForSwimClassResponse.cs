namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;

public sealed record RegisterForSwimClassResponse(
    Guid RegistrationId,
    Guid ClassId,
    string RegistrationStatus,
    Guid OrderId,
    string OrderStatus,
    decimal Amount,
    string TransactionRef,
    DateTime? ExpiresAtUtc,
    DateTime ServerTimeUtc,
    bool ExistingOrder = false);
