using MediatR;

namespace SmartPool.Application.Features.Profiles.Queries.GetMyProfile;

public sealed class GetMyProfileQuery : IRequest<GetMyProfileResponse>
{
    public Guid UserId { get; set; }
}
