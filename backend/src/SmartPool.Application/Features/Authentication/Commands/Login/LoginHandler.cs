using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartPool.Application.Features.Authentication.Commands.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, IActionResult>
{
    private static readonly Regex PhonePattern = new(@"^(?:0|\+84)[35789][0-9]{8}$", RegexOptions.Compiled);

    private readonly IUserOperations _userOperations;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserRoleMembershipService? _membership;
    private readonly IValidator<LoginCommand> _validator;

    public LoginHandler(
        IUserOperations userOperations,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IValidator<LoginCommand> validator,
        IUserRoleMembershipService? membership = null)
    {
        _userOperations = userOperations;
        _passwordHasher = passwordHasher;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _validator = validator;
        _membership = membership;
    }

    public async Task<IActionResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var candidates = await _userOperations.FindUsersByIdentifierAsync(identifier, cancellationToken);

        var matches = candidates.Where(candidate => IsEligible(candidate) &&
            _passwordHasher.VerifyPassword(candidate, candidate.PasswordHash!, request.Password)).ToList();
        if (matches.Count != 1)
            return InvalidCredentials();
        var authenticatedUser = matches[0];

        if (authenticatedUser.RoleId is null)
            return InvalidCredentials();

        var role = await _userOperations.GetRoleByIdAsync(authenticatedUser.RoleId.Value, cancellationToken);
        if (role is null || role.IsDeleted == true)
            return InvalidCredentials();

        var refreshToken = await _refreshTokenService.CreateAsync(authenticatedUser.Id,
            authenticatedUser.PasswordHash!, cancellationToken);
        if (refreshToken is null)
            return InvalidCredentials();
        IReadOnlyList<string> roles = _membership is null ? [role.Name] :
            await _membership.GetActiveRolesAsync(authenticatedUser.Id, role.Name, cancellationToken);
        var token = _membership is null
            ? _accessTokenService.CreateToken(authenticatedUser, role.Name)
            : _accessTokenService.CreateToken(authenticatedUser, role.Name, roles);
        return new OkObjectResult(new LoginResponse
        {
            Id = authenticatedUser.Id,
            Username = authenticatedUser.Username ?? string.Empty,
            Role = role.Name,
            Roles = roles,
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc
        });
    }

    private static bool IsEligible(User user) => user.IsDeleted != true &&
        string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrEmpty(user.PasswordHash);

    private static IActionResult InvalidCredentials() => new ObjectResult(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Thông tin đăng nhập không hợp lệ."
    })
    {
        StatusCode = StatusCodes.Status401Unauthorized,
        ContentTypes = { "application/problem+json" }
    };
}
