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
        if (currentUser.UserId is not { } adminId ||
            !(currentUser.IsInRole("ADMIN") ||
              string.Equals(currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Admin được tạo lớp dạy bơi.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CreateAsync(request, adminId, cancellationToken);
    }
}
