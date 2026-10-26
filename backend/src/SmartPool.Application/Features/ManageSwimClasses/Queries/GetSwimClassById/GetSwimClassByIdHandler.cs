using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;

public sealed class GetSwimClassByIdHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser)
    : IRequestHandler<GetSwimClassByIdQuery, SwimClassDetailResponse>
{
    public async Task<SwimClassDetailResponse> Handle(
        GetSwimClassByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            string.IsNullOrWhiteSpace(currentUser.Role) ||
            !new[] { "ADMIN", "CUSTOMER", "COACH" }.Contains(
                currentUser.Role, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Không có quyền xem lớp dạy bơi.");

        return await operations.GetByIdAsync(
            request.ClassId, userId, currentUser.Role.ToUpperInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
    }
}
