using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

public sealed class ReviewClassAttendanceHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<ReviewClassAttendanceCommand> validator)
    : IRequestHandler<ReviewClassAttendanceCommand, ReviewClassAttendanceResponse>
{
    public async Task<ReviewClassAttendanceResponse> Handle(
        ReviewClassAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được duyệt điểm danh.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.ReviewClassAttendanceAsync(request, coachUserId,
            cancellationToken);
    }
}
