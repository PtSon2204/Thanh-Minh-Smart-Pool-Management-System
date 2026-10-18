using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Authentication.Commands.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand, IActionResult>
{
    private readonly IRefreshTokenService _tokens;
    private readonly IValidator<LogoutCommand> _validator;

    public LogoutHandler(IRefreshTokenService tokens, IValidator<LogoutCommand> validator)
    {
        _tokens = tokens;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => "refreshToken")
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        await _tokens.RevokeAsync(request.RefreshToken, cancellationToken);
        return new NoContentResult();
    }
}
