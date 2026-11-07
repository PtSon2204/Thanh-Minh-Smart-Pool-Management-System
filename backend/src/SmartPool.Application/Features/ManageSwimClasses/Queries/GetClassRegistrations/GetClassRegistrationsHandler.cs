using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrations;

public sealed class GetClassRegistrationsHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetClassRegistrationsQuery> validator)
    : IRequestHandler<GetClassRegistrationsQuery, PagedResponse<ClassRegistrationResponse>>
{
    public async Task<PagedResponse<ClassRegistrationResponse>> Handle(
        GetClassRegistrationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được xem danh sách đăng ký.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetClassRegistrationsAsync(request, coachUserId, cancellationToken);
    }
}
