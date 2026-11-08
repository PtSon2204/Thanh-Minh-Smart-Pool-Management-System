using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassAttendances;

public sealed class GetMyClassAttendancesHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetMyClassAttendancesQuery> validator)
    : IRequestHandler<GetMyClassAttendancesQuery, PagedResponse<MyClassAttendanceItem>>
{
    public async Task<PagedResponse<MyClassAttendanceItem>> Handle(
        GetMyClassAttendancesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerUserId ||
            !currentUser.IsInRole("CUSTOMER") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Customer được xem điểm danh của mình.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetMyAttendancesAsync(request.ClassId, customerUserId,
            request.PageIndex, request.PageSize, cancellationToken);
    }
}
