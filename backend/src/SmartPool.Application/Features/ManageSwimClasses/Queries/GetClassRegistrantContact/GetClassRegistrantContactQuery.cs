using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

public sealed record GetClassRegistrantContactQuery(Guid ClassId, Guid UserId)
    : IRequest<GetClassRegistrantContactResponse>;
