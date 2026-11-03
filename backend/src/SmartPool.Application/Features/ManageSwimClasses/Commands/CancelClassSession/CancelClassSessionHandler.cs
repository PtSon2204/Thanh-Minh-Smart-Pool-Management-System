using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;

public sealed class CancelClassSessionHandler(
    ISwimClassOperations operations, ICurrentUser currentUser,
    IValidator<CancelClassSessionCommand> validator)
    : IRequestHandler<CancelClassSessionCommand, CancelClassSessionResponse>
{
    public async Task<CancelClassSessionResponse> Handle(
        CancelClassSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH"))
            throw new UnauthorizedAccessException("Chỉ Coach được hủy buổi học.");
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CancelSessionAsync(request, coachId, cancellationToken);
    }
}
