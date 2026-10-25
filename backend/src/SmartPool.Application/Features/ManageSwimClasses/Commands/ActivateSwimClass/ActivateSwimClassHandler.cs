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
        if (currentUser.UserId is null ||
            !(currentUser.IsInRole("ADMIN") ||
              string.Equals(currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Admin được kích hoạt lớp dạy bơi.");

        return operations.ActivateAsync(request.ClassId, cancellationToken);
    }
}
