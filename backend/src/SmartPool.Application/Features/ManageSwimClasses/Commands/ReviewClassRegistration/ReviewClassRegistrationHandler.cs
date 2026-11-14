using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

public sealed class ReviewClassRegistrationHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<ReviewClassRegistrationCommand> validator)
    : IRequestHandler<ReviewClassRegistrationCommand, ReviewClassRegistrationResponse>
{
    public async Task<ReviewClassRegistrationResponse> Handle(
        ReviewClassRegistrationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachUserId ||
            !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được duyệt đăng ký.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.ReviewClassRegistrationAsync(request, coachUserId,
            cancellationToken);
    }
}
