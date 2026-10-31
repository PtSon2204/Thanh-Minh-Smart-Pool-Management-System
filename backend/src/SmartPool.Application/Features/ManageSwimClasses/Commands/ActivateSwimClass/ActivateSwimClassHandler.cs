using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ActivateSwimClass;

public sealed class ActivateSwimClassHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser)
    : IRequestHandler<ActivateSwimClassCommand, ActivateSwimClassResponse>
{
    public Task<ActivateSwimClassResponse> Handle(
        ActivateSwimClassCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH"))
            throw new UnauthorizedAccessException("Chỉ Coach được kích hoạt lớp dạy bơi.");

        return operations.ActivateAsync(request.ClassId, coachId, cancellationToken);
    }
}
