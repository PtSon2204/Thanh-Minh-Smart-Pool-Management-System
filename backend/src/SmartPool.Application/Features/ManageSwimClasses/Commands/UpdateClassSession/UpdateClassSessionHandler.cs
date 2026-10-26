using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

public sealed class UpdateClassSessionHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<UpdateClassSessionCommand> validator)
    : IRequestHandler<UpdateClassSessionCommand, UpdateClassSessionResponse>
{
    public async Task<UpdateClassSessionResponse> Handle(
        UpdateClassSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null ||
            !(currentUser.IsInRole("ADMIN") ||
              string.Equals(currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Admin được cập nhật buổi học.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.UpdateSessionAsync(request, cancellationToken);
    }
}
