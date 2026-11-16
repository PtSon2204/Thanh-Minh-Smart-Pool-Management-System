using MediatR;

namespace SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;

public sealed record GetStaffEditQuery(Guid UserId, Guid ActorId) : IRequest<GetStaffEditResponse>;
