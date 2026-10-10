using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;

public sealed class ChangeUserRoleHandler : IRequestHandler<ChangeUserRoleCommand, IActionResult>
{
    private readonly IUserOperations _users;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ChangeUserRoleCommand> _validator;

    public ChangeUserRoleHandler(IUserOperations users, ICurrentUser currentUser,
        IValidator<ChangeUserRoleCommand> validator)
    {
        _users = users;
        _currentUser = currentUser;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
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

        var requestedRole = Enum.Parse<RoleEnum>(request.Role.Trim(), true);
        try
        {
            var changed = await _users.ChangeUserRoleAsync(request.Id, actorId, requestedRole, cancellationToken);
            return changed is null
                ? new NotFoundObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy người dùng."
                })
                : new OkObjectResult(changed);
        }
        catch (RoleChangeForbiddenException)
        {
            return new ForbidResult();
        }
        catch (RoleChangeConflictException exception)
        {
            return new ConflictObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Không thể thay đổi vai trò.",
                Detail = exception.Message
            });
        }
    }

}

public sealed class RoleChangeConflictException(string message) : Exception(message);
public sealed class RoleChangeForbiddenException : Exception { }
