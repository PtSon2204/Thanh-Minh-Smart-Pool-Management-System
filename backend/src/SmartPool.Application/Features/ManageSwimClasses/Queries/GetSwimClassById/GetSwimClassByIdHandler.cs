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
            !new[] { "CUSTOMER", "COACH" }.Contains(
                currentUser.Role, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Không có quyền xem lớp dạy bơi.");

        var role = request.Scope?.Trim().ToUpperInvariant() ?? currentUser.Role.ToUpperInvariant();
        if (role is not ("COACH" or "CUSTOMER") ||
            (!string.IsNullOrWhiteSpace(request.Scope) && !currentUser.IsInRole(role)))
            throw new UnauthorizedAccessException("Không có quyền xem phạm vi lớp được yêu cầu.");
        return await operations.GetByIdAsync(
            request.ClassId, userId, role, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
    }
}
