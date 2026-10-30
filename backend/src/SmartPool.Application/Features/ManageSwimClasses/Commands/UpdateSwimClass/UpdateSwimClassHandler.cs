using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

public sealed class UpdateSwimClassHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<UpdateSwimClassCommand> validator)
    : IRequestHandler<UpdateSwimClassCommand, UpdateSwimClassResponse>
{
    public async Task<UpdateSwimClassResponse> Handle(
        UpdateSwimClassCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH"))
            throw new UnauthorizedAccessException("Chỉ Coach được cập nhật lớp dạy bơi.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.UpdateAsync(request, coachId, cancellationToken);
    }
}
