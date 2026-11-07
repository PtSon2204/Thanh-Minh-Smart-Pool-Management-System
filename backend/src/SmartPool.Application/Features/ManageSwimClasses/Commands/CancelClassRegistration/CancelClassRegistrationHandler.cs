using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassRegistration;

public sealed class CancelClassRegistrationHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<CancelClassRegistrationCommand> validator)
    : IRequestHandler<CancelClassRegistrationCommand, CancelClassRegistrationResponse>
{
    public async Task<CancelClassRegistrationResponse> Handle(
        CancelClassRegistrationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được hủy đăng ký.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CancelClassRegistrationAsync(request, coachUserId, cancellationToken);
    }
}
