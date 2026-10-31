using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

public sealed class GetSwimClassesHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetSwimClassesQuery> validator)
    : IRequestHandler<GetSwimClassesQuery, PagedResponse<SwimClassListItem>>
{
    public async Task<PagedResponse<SwimClassListItem>> Handle(
        GetSwimClassesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            string.IsNullOrWhiteSpace(currentUser.Role) ||
            !new[] { "CUSTOMER", "COACH" }.Contains(
                currentUser.Role, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Không có quyền xem danh sách lớp dạy bơi.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var role = request.Scope?.Trim().ToUpperInvariant() ?? currentUser.Role.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.Scope) && !currentUser.IsInRole(role))
            throw new UnauthorizedAccessException("Không có quyền xem phạm vi lớp được yêu cầu.");
        return await operations.GetListAsync(request, userId, role, cancellationToken);
    }
}
