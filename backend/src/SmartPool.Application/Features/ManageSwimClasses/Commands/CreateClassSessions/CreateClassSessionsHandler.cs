using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed class CreateClassSessionsHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<CreateClassSessionsCommand> validator)
    : IRequestHandler<CreateClassSessionsCommand, CreateClassSessionsResponse>
{
    public async Task<CreateClassSessionsResponse> Handle(
        CreateClassSessionsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null ||
            !(currentUser.IsInRole("ADMIN") ||
              string.Equals(currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chỉ Admin được tạo lịch lớp dạy bơi.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CreateSessionsAsync(request, cancellationToken);
    }
}
