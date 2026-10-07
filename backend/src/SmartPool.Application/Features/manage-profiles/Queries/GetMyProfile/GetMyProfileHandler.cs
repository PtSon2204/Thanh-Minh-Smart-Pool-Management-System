using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Profiles.Queries.GetMyProfile;

public sealed class GetMyProfileHandler : IRequestHandler<GetMyProfileQuery, GetMyProfileResponse>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    
    public GetMyProfileHandler(IRepository<User> users, IRepository<UserProfile> profiles)
    {
        _users = users;
        _profiles = profiles;
    }

    public async Task<GetMyProfileResponse> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("Tài khoản không tồn tại.");

        var profile = await _profiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        return new GetMyProfileResponse
        {
            Id = user.Id,
            Username = user.Username ?? "",
            Email = user.Email ?? "",
            Phone = user.Phone ?? "",
            FullName = profile?.FullName ?? "",
            AvatarUrl = profile?.AvatarUrl,
            Address = profile?.Address,
            DateOfBirth = profile?.DateOfBirth
        };
    }
}
