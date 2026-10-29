using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;

public sealed record GetSwimClassByIdQuery(Guid ClassId, string? Scope = null) : IRequest<SwimClassDetailResponse>;
