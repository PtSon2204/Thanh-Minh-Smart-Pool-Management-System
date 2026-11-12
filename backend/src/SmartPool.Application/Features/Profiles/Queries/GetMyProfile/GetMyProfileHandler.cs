using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Profiles.Queries.GetMyProfile;

public sealed class GetMyProfileHandler : IRequestHandler<GetMyProfileQuery, GetMyProfileResponse>
{
    private readonly IUserOperations _userOperations;
    
    public GetMyProfileHandler(IUserOperations userOperations)
    {
        _userOperations = userOperations;
    }

    public async Task<GetMyProfileResponse> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userOperations.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("Tài khoản không tồn tại.");

        var profile = await _userOperations.GetUserProfileByUserIdAsync(request.UserId, cancellationToken);

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
