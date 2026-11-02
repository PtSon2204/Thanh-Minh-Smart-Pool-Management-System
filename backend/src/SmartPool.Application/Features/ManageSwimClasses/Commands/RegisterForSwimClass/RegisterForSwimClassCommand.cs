using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;

public sealed record RegisterForSwimClassCommand(Guid ClassId) : IRequest<RegisterForSwimClassResponse>;
