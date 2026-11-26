using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CompleteSwimClass;

public sealed class CompleteSwimClassHandler(ISwimClassOperations operations, ICurrentUser currentUser)
    : IRequestHandler<CompleteSwimClassCommand, string>
{
    public Task<string> Handle(CompleteSwimClassCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach quản lý lớp được kết thúc lớp.");
        return operations.CompleteClassAsync(request.ClassId, coachId, cancellationToken);
    }
}
