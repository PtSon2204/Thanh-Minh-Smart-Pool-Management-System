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
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<Notification> _notifications;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IValidator<GoogleRegisterCommand> _validator;

    public GoogleRegisterHandler(IGoogleIdTokenVerifier verifier, IRepository<User> users,
        IRepository<UserProfile> profiles, IRepository<Role> roles,
        IRepository<Notification> notifications, INotificationService notificationService,
        IUnitOfWork unitOfWork,
        IAccessTokenService accessTokens, IRefreshTokenService refreshTokens,
        IValidator<GoogleRegisterCommand> validator)
    {
        _verifier = verifier;
        _users = users;
        _profiles = profiles;
        _roles = roles;
        _notifications = notifications;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
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
        var conflict = await RegisterHandler.FindConflictAsync(_users, username,
            email, phone, cancellationToken);
        if (conflict is not null)
            return RegisterHandler.CreateConflictResult(conflict);

        var role = await _roles.FirstOrDefaultAsync(candidate =>
            candidate.Name.ToUpper() == nameof(RoleEnum.CUSTOMER) && candidate.IsDeleted != true,
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
            await _users.AddAsync(user, cancellationToken);
            await _profiles.AddAsync(profile, cancellationToken);

            var adminUser = await _users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == "admin", cancellationToken);
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
            await _notifications.AddAsync(notification, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
