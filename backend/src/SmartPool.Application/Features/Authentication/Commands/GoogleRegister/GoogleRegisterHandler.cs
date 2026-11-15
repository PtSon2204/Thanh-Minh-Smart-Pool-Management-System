using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.Authentication.Commands.GoogleRegister;

public sealed class GoogleRegisterHandler : IRequestHandler<GoogleRegisterCommand, IActionResult>
{
    private readonly IGoogleIdTokenVerifier _verifier;
    private readonly IUserOperations _userOperations;
    private readonly INotificationService _notificationService;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IValidator<GoogleRegisterCommand> _validator;

    public GoogleRegisterHandler(IGoogleIdTokenVerifier verifier, IUserOperations userOperations,
        INotificationService notificationService,
        IAccessTokenService accessTokens, IRefreshTokenService refreshTokens,
        IValidator<GoogleRegisterCommand> validator)
    {
        _verifier = verifier;
        _userOperations = userOperations;
        _notificationService = notificationService;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(GoogleRegisterCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        GoogleIdentity? identity;
        try
        {
            identity = await _verifier.VerifyAsync(request.Credential, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "GOOGLE_AUTH_NOT_CONFIGURED",
                "Đăng ký Google chưa được cấu hình.");
        }

        if (identity is null || !identity.EmailVerified)
            return Problem(StatusCodes.Status401Unauthorized, "INVALID_GOOGLE_CREDENTIAL",
                "Thông tin xác thực Google không hợp lệ.");

        var email = identity.Email.Trim().ToLowerInvariant();
        if (!email.EndsWith("@gmail.com", StringComparison.Ordinal) || email.Length > 150)
            return Problem(StatusCodes.Status403Forbidden, "GOOGLE_EMAIL_UNSUPPORTED",
                "Hiện chỉ hỗ trợ tài khoản Google dùng địa chỉ Gmail.");

        var username = request.Username.Trim().ToLowerInvariant();
        var inputPhone = request.Phone.Trim();
        var phone = inputPhone.StartsWith("+84", StringComparison.Ordinal)
            ? "0" + inputPhone[3..] : inputPhone;
        var conflict = await RegisterHandler.FindConflictAsync(_userOperations, username,
            email, phone, cancellationToken);
        if (conflict is not null)
            return RegisterHandler.CreateConflictResult(conflict);

        var role = await _userOperations.GetRoleByNameAsync(
            nameof(RoleEnum.CUSTOMER),
            cancellationToken);
        if (role is null)
            return Problem(StatusCodes.Status503ServiceUnavailable, "CUSTOMER_ROLE_NOT_CONFIGURED",
                "Đăng ký Google chưa sẵn sàng.");

        var user = new User
        {
            Id = Guid.NewGuid(), Username = username, Email = email, Phone = phone,
            PasswordHash = null, RoleId = role.Id, Status = "Active", IsDeleted = false
        };
        var profile = new UserProfile
        {
            UserId = user.Id, User = user, FullName = request.FullName.Trim()
        };
        user.UserProfile = profile;

        try
        {
            var adminUser = await _userOperations.GetUserByUsernameAsync("admin", cancellationToken);
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = adminUser?.Id ?? user.Id,
                Title = "Khách hàng mới đăng ký (Google)",
                Content = $"Khách hàng {profile.FullName} ({user.Username}) đã đăng ký tài khoản thành công qua Google.",
                Type = "CUSTOMER",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _userOperations.CreateUserWithProfileAsync(user, profile, notification, cancellationToken);

            try
            {
                var payload = new
                {
                    id = notification.Id,
                    userId = notification.UserId,
                    title = notification.Title,
                    content = notification.Content,
                    type = notification.Type,
                    isRead = false,
                    createdAt = notification.CreatedAt
                };
                await _notificationService.SendToAllAsync("ReceiveNotification", payload, cancellationToken);
            }
            catch
            {
                // Transient broadcast error should not fail registration
            }
        }
        catch (UniqueUserFieldException exception)
        {
            return RegisterHandler.CreateConflictResult(exception.Field);
        }

        var refresh = await _refreshTokens.CreateAsync(user.Id, null, cancellationToken);
        if (refresh is null)
            return Problem(StatusCodes.Status503ServiceUnavailable, "SESSION_CREATION_FAILED",
                "Tài khoản đã được tạo nhưng chưa thể đăng nhập. Vui lòng thử lại.");
        var access = _accessTokens.CreateToken(user, role.Name);

        return new OkObjectResult(new LoginResponse
        {
            Id = user.Id, Username = username, Role = role.Name, Roles = [role.Name],
            AccessToken = access.Token, ExpiresAtUtc = access.ExpiresAtUtc,
            RefreshToken = refresh.Token, RefreshTokenExpiresAtUtc = refresh.ExpiresAtUtc
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
