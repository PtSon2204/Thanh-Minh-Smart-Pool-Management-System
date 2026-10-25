using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;

public sealed record ActivateSwimClassCommand(Guid ClassId) : IRequest<ActivateSwimClassResponse>;

public sealed record ActivateSwimClassResponse(Guid Id, string Status, DateTime UpdatedAt);
