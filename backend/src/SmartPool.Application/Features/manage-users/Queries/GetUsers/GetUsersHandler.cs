using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, IActionResult>
{
    private readonly IUserOperations _users;
    private readonly IValidator<GetUsersQuery> _validator;

    public GetUsersHandler(IUserOperations users, IValidator<GetUsersQuery> validator)
    {
        _users = users;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        return new OkObjectResult(await _users.GetUsersAsync(request, cancellationToken));
    }
}
