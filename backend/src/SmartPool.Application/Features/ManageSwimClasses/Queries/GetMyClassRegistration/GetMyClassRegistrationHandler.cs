using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetMyClassRegistration;

public sealed class GetMyClassRegistrationHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyClassRegistrationQuery, MyClassRegistrationResponse>
{
    public async Task<MyClassRegistrationResponse> Handle(
        GetMyClassRegistrationQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerUserId || !currentUser.IsInRole("CUSTOMER"))
            throw new UnauthorizedAccessException("Chỉ Customer được xem đăng ký lớp của mình.");

        var registration = await operations.GetMyRegistrationAsync(
            request.ClassId, customerUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Bạn chưa đăng ký lớp này.");
        return new MyClassRegistrationResponse(registration.Id, registration.ClassId,
            registration.Status, registration.RegisteredAt, registration.ConfirmedAt,
            registration.CancelledAt);
    }
}
