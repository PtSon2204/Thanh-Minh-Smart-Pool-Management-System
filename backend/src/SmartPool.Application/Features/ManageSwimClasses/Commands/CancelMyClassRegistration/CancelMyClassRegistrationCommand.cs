using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;

public sealed record CancelMyClassRegistrationCommand(Guid ClassId)
    : IRequest<CancelMyClassRegistrationResponse>;

public sealed record CancelMyClassRegistrationResponse(
    Guid RegistrationId, Guid ClassId, string Status,
    Guid CancelledByUserId, DateTime CancelledAt);
