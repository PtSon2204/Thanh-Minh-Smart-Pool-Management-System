using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Authentication.Commands.Refresh;

public sealed class RefreshHandler : IRequestHandler<RefreshCommand, IActionResult>
{
    private readonly IRefreshTokenService _tokens;
    private readonly IValidator<RefreshCommand> _validator;

    public RefreshHandler(IRefreshTokenService tokens, IValidator<RefreshCommand> validator)
    {
        _tokens = tokens;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => "refreshToken")
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        var result = await _tokens.RotateAsync(request.RefreshToken, cancellationToken);
        if (result is null)
            return new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh token không hợp lệ hoặc đã hết hạn."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ContentTypes = { "application/problem+json" }
            };

        return new OkObjectResult(new RefreshResponse
        {
            Id = result.Id,
            Username = result.Username,
            Role = result.Role,
            Roles = result.Roles,
            AccessToken = result.AccessToken,
            ExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            RefreshToken = result.RefreshToken,
            RefreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc
        });
    }
}
