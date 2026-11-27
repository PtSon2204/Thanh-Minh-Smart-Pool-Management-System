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
    private readonly IStaffContractAccessPolicy _contractAccess;
    private readonly IUserRoleMembershipService? _membership;
    private readonly IValidator<LoginCommand> _validator;

    public LoginHandler(
        IUserOperations userOperations,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IValidator<LoginCommand> validator,
        IStaffContractAccessPolicy contractAccess,
        IUserRoleMembershipService? membership = null)
    {
        _userOperations = userOperations;
        _passwordHasher = passwordHasher;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _validator = validator;
        _contractAccess = contractAccess;
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
        var authenticatedUser = await _userOperations.FindUserByIdentifierAsync(identifier, cancellationToken);
        if (authenticatedUser is null || !IsEligible(authenticatedUser) ||
            !_passwordHasher.VerifyPassword(authenticatedUser, authenticatedUser.PasswordHash!, request.Password))
            return InvalidCredentials();

        if (authenticatedUser.RoleId is null)
            return InvalidCredentials();

        var role = await _userOperations.GetRoleByIdAsync(authenticatedUser.RoleId.Value, cancellationToken);
        if (role is null || role.IsDeleted == true)
            return InvalidCredentials();

        var contractAccess = await _contractAccess.EvaluateAsync(authenticatedUser.Id, role.Name, cancellationToken);
        if (authenticatedUser.MustChangePassword)
        {
            // Credential setup is separated from business authorization: a valid,
            // unexpired temporary credential may establish a restricted session
            // before the contract starts. Only the NotStarted state is bypassed.
            if (authenticatedUser.TemporaryPasswordExpiresAtUtc is not { } expiry || expiry <= DateTime.UtcNow)
                return new UnauthorizedObjectResult(new { code = "TEMPORARY_PASSWORD_EXPIRED",
                    message = "Mật khẩu tạm thời đã hết hạn. Hãy liên hệ Admin." });
            if (!contractAccess.IsAllowed && contractAccess.State != StaffContractState.NotStarted)
                return InvalidCredentials();
            var restricted = _accessTokenService.CreatePasswordChangeToken(authenticatedUser, role.Name);
            return new OkObjectResult(new LoginResponse
            {
                Id = authenticatedUser.Id, Username = authenticatedUser.Username ?? string.Empty,
                Role = role.Name, Roles = [role.Name], MustChangePassword = true,
                AccessToken = restricted.Token, ExpiresAtUtc = restricted.ExpiresAtUtc
            });
        }

        if (!contractAccess.IsAllowed)
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
