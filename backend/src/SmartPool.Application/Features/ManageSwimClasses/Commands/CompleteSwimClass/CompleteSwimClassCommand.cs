using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CompleteSwimClass;

public sealed record CompleteSwimClassCommand(Guid ClassId) : IRequest<string>;
