using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

public sealed class CreateSwimClassHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<CreateSwimClassCommand> validator)
    : IRequestHandler<CreateSwimClassCommand, CreateSwimClassResponse>
{
    public async Task<CreateSwimClassResponse> Handle(
        CreateSwimClassCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId)
            throw new UnauthorizedAccessException("Cần đăng nhập để tạo lớp dạy bơi.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CreateAsync(request, coachId, cancellationToken);
    }
}
