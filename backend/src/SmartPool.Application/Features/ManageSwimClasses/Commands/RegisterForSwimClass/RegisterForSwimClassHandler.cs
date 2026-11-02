using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;

public sealed class RegisterForSwimClassHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser)
    : IRequestHandler<RegisterForSwimClassCommand, RegisterForSwimClassResponse>
{
    public Task<RegisterForSwimClassResponse> Handle(
        RegisterForSwimClassCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerUserId ||
            !(currentUser.IsInRole("CUSTOMER") ||
              string.Equals(currentUser.Role, "CUSTOMER", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Customer được đăng ký lớp dạy bơi.");

        if (request.ClassId == Guid.Empty)
            throw new ArgumentException("Mã lớp không hợp lệ.", nameof(request));

        return operations.RegisterAsync(request.ClassId, customerUserId, cancellationToken);
    }
}
