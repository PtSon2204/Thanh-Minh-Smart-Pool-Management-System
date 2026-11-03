using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

public sealed class DeactivateSwimClassHandler(
    ISwimClassOperations operations, ICurrentUser currentUser,
    IValidator<DeactivateSwimClassCommand> validator)
    : IRequestHandler<DeactivateSwimClassCommand, DeactivateSwimClassResponse>
{
    public async Task<DeactivateSwimClassResponse> Handle(
        DeactivateSwimClassCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH"))
            throw new UnauthorizedAccessException("Chỉ Coach được ngừng lớp dạy bơi.");
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.DeactivateAsync(request, coachId, cancellationToken);
    }
}
