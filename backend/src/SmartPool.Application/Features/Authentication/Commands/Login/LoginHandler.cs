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

    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IValidator<LoginCommand> _validator;

    public LoginHandler(
        IRepository<User> users,
        IRepository<Role> roles,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokenService,
        IValidator<LoginCommand> validator)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _accessTokenService = accessTokenService;
        _validator = validator;
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
        IEnumerable<User> candidates;
        if (identifier.Contains('@'))
        {
            var user = await _users.FirstOrDefaultAsync(
                candidate => candidate.Email != null && candidate.Email.ToLower() == identifier,
                cancellationToken);
            candidates = user is null ? [] : [user];
        }
        else if (PhonePattern.IsMatch(identifier))
        {
            var localPhone = identifier.StartsWith("+84", StringComparison.Ordinal)
                ? "0" + identifier[3..]
                : identifier;
            var internationalPhone = "+84" + localPhone[1..];
            // A registered username may also look like a phone number. Check both accounts.
            candidates = await _users.FindAsync(
                candidate => candidate.Phone == localPhone || candidate.Phone == internationalPhone ||
                    (candidate.Username != null && candidate.Username.ToLower() == identifier),
                cancellationToken);
        }
        else
        {
            var user = await _users.FirstOrDefaultAsync(
                candidate => candidate.Username != null && candidate.Username.ToLower() == identifier,
                cancellationToken);
            candidates = user is null ? [] : [user];
        }

        var matches = candidates.Where(candidate => IsEligible(candidate) &&
            _passwordHasher.VerifyPassword(candidate, candidate.PasswordHash!, request.Password)).ToList();
        if (matches.Count != 1)
            return InvalidCredentials();
        var authenticatedUser = matches[0];

        if (authenticatedUser.RoleId is null)
            return InvalidCredentials();

        var role = await _roles.GetByIdAsync(authenticatedUser.RoleId.Value, cancellationToken);
        if (role is null || role.IsDeleted == true)
            return InvalidCredentials();

        var token = _accessTokenService.CreateToken(authenticatedUser, role.Name);
        return new OkObjectResult(new LoginResponse
        {
            Id = authenticatedUser.Id,
            Username = authenticatedUser.Username ?? string.Empty,
            Role = role.Name,
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc
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
