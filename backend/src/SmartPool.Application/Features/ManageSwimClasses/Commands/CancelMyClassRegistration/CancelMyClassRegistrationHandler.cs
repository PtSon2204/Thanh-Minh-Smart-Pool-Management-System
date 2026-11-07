using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;

public sealed class CancelMyClassRegistrationHandler(
    ISwimClassOperations operations, ICurrentUser currentUser)
    : IRequestHandler<CancelMyClassRegistrationCommand, CancelMyClassRegistrationResponse>
{
    public Task<CancelMyClassRegistrationResponse> Handle(
        CancelMyClassRegistrationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerId || !currentUser.IsInRole("CUSTOMER"))
            throw new UnauthorizedAccessException("Chỉ Customer được tự hủy đăng ký lớp.");
        if (request.ClassId == Guid.Empty)
            throw new ArgumentException("Mã lớp không hợp lệ.", nameof(request));
        return operations.CancelMyRegistrationAsync(request.ClassId, customerId, cancellationToken);
    }
}
