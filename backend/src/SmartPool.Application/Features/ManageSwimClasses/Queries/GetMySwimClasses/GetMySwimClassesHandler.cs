using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMySwimClasses;

public sealed class GetMySwimClassesHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<GetMySwimClassesQuery> validator)
    : IRequestHandler<GetMySwimClassesQuery, PagedResponse<MySwimClassItem>>
{
    public async Task<PagedResponse<MySwimClassItem>> Handle(
        GetMySwimClassesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerUserId ||
            !(currentUser.IsInRole("CUSTOMER") ||
              string.Equals(currentUser.Role, "CUSTOMER", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Customer được xem lớp học của mình.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetMyClassesAsync(request, customerUserId, cancellationToken);
    }
}
