using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassSessionAttendances;

public sealed class GetClassSessionAttendancesHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetClassSessionAttendancesQuery> validator)
    : IRequestHandler<GetClassSessionAttendancesQuery, PagedResponse<ClassSessionAttendanceItem>>
{
    public async Task<PagedResponse<ClassSessionAttendanceItem>> Handle(
        GetClassSessionAttendancesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được xem điểm danh.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetClassSessionAttendancesAsync(request, coachUserId,
            cancellationToken);
    }
}
