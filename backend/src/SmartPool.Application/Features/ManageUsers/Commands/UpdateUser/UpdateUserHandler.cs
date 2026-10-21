using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Interfaces.Repositories;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand, IActionResult>
{
    private readonly IUserOperations _users;
    private readonly IValidator<UpdateUserCommand> _validator;

    public UpdateUserHandler(IUserOperations users, IValidator<UpdateUserCommand> validator)
    {
        _users = users;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        var rawPhone = request.Phone.Trim();
        var phone = rawPhone.StartsWith("+84", StringComparison.Ordinal)
            ? "0" + rawPhone[3..]
            : rawPhone;
        var values = new UpdateUserValues(
            request.Id,
            request.Email.Trim().ToLowerInvariant(),
            phone,
            request.FullName.Trim(),
            request.DateOfBirth,
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim());

        try
        {
            var updated = await _users.UpdateUserAsync(values, cancellationToken);
            return updated is null
                ? new NotFoundObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy người dùng."
                })
                : new OkObjectResult(updated);
        }
        catch (UniqueUserFieldException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Không thể cập nhật người dùng."
            };
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                [exception.Field] = [exception.Field == "email"
                    ? "Email đã được sử dụng."
                    : "Số điện thoại đã được sử dụng."]
            };
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { "application/problem+json" }
            };
        }
    }
}
