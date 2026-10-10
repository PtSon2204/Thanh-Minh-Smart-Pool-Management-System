using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusHandler : IRequestHandler<ChangeUserStatusCommand, IActionResult>
{
    private readonly IUserOperations _users;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ChangeUserStatusCommand> _validator;

    public ChangeUserStatusHandler(IUserOperations users, ICurrentUser currentUser,
        IValidator<ChangeUserStatusCommand> validator)
    {
        _users = users;
        _currentUser = currentUser;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(ChangeUserStatusCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        if (_currentUser.UserId is not { } actorId || !_currentUser.IsInRole(nameof(RoleEnum.ADMIN)))
            return new ForbidResult();

        var status = Enum.Parse<UserStatusEnum>(request.Status.Trim(), true);
        try
        {
            var changed = await _users.ChangeUserStatusAsync(request.Id, actorId, status, cancellationToken);
            return changed is null
                ? new NotFoundObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy người dùng."
                })
                : new OkObjectResult(changed);
        }
        catch (StatusChangeForbiddenException)
        {
            return new ForbidResult();
        }
        catch (StatusChangeConflictException exception)
        {
            return new ConflictObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Không thể thay đổi trạng thái tài khoản.",
                Detail = exception.Message
            });
        }
    }
}

public sealed class StatusChangeConflictException(string message) : Exception(message);
public sealed class StatusChangeForbiddenException : Exception { }
