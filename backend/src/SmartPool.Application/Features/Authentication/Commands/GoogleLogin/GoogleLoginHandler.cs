using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Authentication.Commands.GoogleLogin;

public sealed class GoogleLoginHandler : IRequestHandler<GoogleLoginCommand, IActionResult>
{
    private readonly IGoogleIdTokenVerifier _verifier;
    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUserRoleMembershipService? _membership;

    public GoogleLoginHandler(IGoogleIdTokenVerifier verifier, IRepository<User> users,
        IRepository<Role> roles, IAccessTokenService accessTokens, IRefreshTokenService refreshTokens,
        IUserRoleMembershipService? membership = null)
    {
        _verifier = verifier;
        _users = users;
        _roles = roles;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
        _membership = membership;
    }

    public async Task<IActionResult> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Credential) || request.Credential.Length > 8192)
            return new BadRequestObjectResult(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["credential"] = ["Google credential không hợp lệ."]
            }));

        GoogleIdentity? identity;
        try
        {
            identity = await _verifier.VerifyAsync(request.Credential, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "GOOGLE_AUTH_NOT_CONFIGURED",
                "Đăng nhập Google chưa được cấu hình.");
        }

        if (identity is null || !identity.EmailVerified)
            return Problem(StatusCodes.Status401Unauthorized, "INVALID_GOOGLE_CREDENTIAL",
                "Thông tin xác thực Google không hợp lệ.");

        var email = identity.Email.Trim().ToLowerInvariant();
        if (!email.EndsWith("@gmail.com", StringComparison.Ordinal) || email.Length > 150)
            return Problem(StatusCodes.Status403Forbidden, "GOOGLE_EMAIL_UNSUPPORTED",
                "Hiện chỉ hỗ trợ tài khoản Google dùng địa chỉ Gmail.");

        var user = await _users.FirstOrDefaultAsync(candidate =>
            candidate.Email != null && candidate.Email.ToLower() == email, cancellationToken);
        if (user is null)
            return Problem(StatusCodes.Status409Conflict, "GOOGLE_ACCOUNT_NOT_REGISTERED",
                "Tài khoản Google chưa được đăng ký.");
        if (user.PasswordHash is not null)
            return Problem(StatusCodes.Status409Conflict, "EMAIL_ALREADY_REGISTERED",
                "Email đã thuộc tài khoản dùng mật khẩu.");
        if (user.IsDeleted == true || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || user.RoleId is null || string.IsNullOrWhiteSpace(user.Username))
            return Problem(StatusCodes.Status401Unauthorized, "ACCOUNT_UNAVAILABLE",
                "Tài khoản không khả dụng.");

        var role = await _roles.GetByIdAsync(user.RoleId.Value, cancellationToken);
        if (role is null || role.IsDeleted == true ||
            !string.Equals(role.Name, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            return Problem(StatusCodes.Status401Unauthorized, "ACCOUNT_UNAVAILABLE",
                "Tài khoản không khả dụng.");

        var refresh = await _refreshTokens.CreateAsync(user.Id, null, cancellationToken);
        if (refresh is null)
            return Problem(StatusCodes.Status401Unauthorized, "ACCOUNT_UNAVAILABLE",
                "Tài khoản không khả dụng.");
        IReadOnlyList<string> roles = _membership is null ? [role.Name] :
            await _membership.GetActiveRolesAsync(user.Id, role.Name, cancellationToken);
        var access = _membership is null
            ? _accessTokens.CreateToken(user, role.Name)
            : _accessTokens.CreateToken(user, role.Name, roles);

        return new OkObjectResult(new LoginResponse
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            Role = role.Name,
            Roles = roles,
            AccessToken = access.Token,
            ExpiresAtUtc = access.ExpiresAtUtc,
            RefreshToken = refresh.Token,
            RefreshTokenExpiresAtUtc = refresh.ExpiresAtUtc
        });
    }

    private static IActionResult Problem(int status, string code, string title)
    {
        var details = new ProblemDetails { Status = status, Title = title };
        details.Extensions["code"] = code;
        return new ObjectResult(details)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
