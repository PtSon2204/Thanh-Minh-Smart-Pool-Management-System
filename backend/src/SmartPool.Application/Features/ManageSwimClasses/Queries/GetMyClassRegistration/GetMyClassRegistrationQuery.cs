using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;

public sealed record GetMyClassRegistrationQuery(Guid ClassId)
    : IRequest<MyClassRegistrationResponse>;
