using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

public sealed class GetClassRegistrantContactHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetClassRegistrantContactQuery> validator)
    : IRequestHandler<GetClassRegistrantContactQuery, GetClassRegistrantContactResponse>
{
    public async Task<GetClassRegistrantContactResponse> Handle(
        GetClassRegistrantContactQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được xem thông tin người đăng ký.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetClassRegistrantContactAsync(request.ClassId,
            request.UserId, coachUserId, cancellationToken);
    }
}
