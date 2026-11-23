using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Commands.CreateUser;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, IActionResult>
{
    private const string CredentialsEmailSubject = "[Thanh Minh Smart Pool] Thông tin tài khoản";
    private readonly IUserOperations _userOperations;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IValidator<CreateUserCommand> _validator;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserOperations userOperations,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IValidator<CreateUserCommand> validator,
        ILogger<CreateUserHandler> logger)
    {
        _userOperations = userOperations;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        var username = NormalizeUsername(request.Username);
        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);
        var roleName = NormalizeRole(request.Role);

        var conflict = await FindConflictAsync(
            username,
            email,
            phone,
            cancellationToken);

        if (conflict is not null)
            return Conflict(conflict.Value.Field, conflict.Value.Message);

        var role = await FindRoleAsync(
            roleName,
            cancellationToken);

        if (role is null)
        {
            return Conflict(
                "role",
                "Vai trò chưa được cấu hình hoặc đã ngừng sử dụng.");
        }

        var generatedPassword = GeneratePassword();
        var user = CreateUser(
            username,
            email,
            phone,
            role.Id,
            generatedPassword);

        var profile = CreateUserProfile(request, user);

        user.UserProfile = profile;

        try
        {
            await _userOperations.CreateUserWithProfileAsync(user, profile, null, cancellationToken);
        }
        catch (UniqueUserFieldException exception)
        {
            return Conflict(
                exception.Field,
                GetConflictMessage(exception.Field));
        }

        try
        {
            await _emailService.SendAsync(
                email, CredentialsEmailSubject,
                BuildCredentialsEmailBody(username, generatedPassword), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Created user {UserId}, but credentials email delivery failed.", user.Id);
            return EmailDeliveryFailed();
        }

        return CreatedResponse(
            user,
            profile,
            roleName);
    }

    
    private User CreateUser(
        string username,
        string email,
        string phone,
        Guid roleId,
        string generatedPassword)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            Phone = phone,
            RoleId = roleId,
            Status = UserStatusEnum.ACTIVE.ToString(),
            IsDeleted = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            generatedPassword);

        return user;
    }

    private static string GeneratePassword() =>
        "Tm1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static string BuildCredentialsEmailBody(string username, string password) =>
        "<p>Tài khoản Thanh Minh Smart Pool của bạn đã được tạo.</p>" +
        $"<p>Tên đăng nhập: <strong>{HtmlEncoder.Default.Encode(username)}</strong></p>" +
        $"<p>Mật khẩu tạm thời: <strong>{HtmlEncoder.Default.Encode(password)}</strong></p>" +
        "<p>Vui lòng đổi mật khẩu sau khi đăng nhập.</p>";

    private static IActionResult EmailDeliveryFailed()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Tài khoản đã được tạo nhưng chưa gửi được email.",
            Detail = "Người dùng có thể dùng chức năng quên mật khẩu để thiết lập mật khẩu mới."
        };
        problem.Extensions["code"] = "ACCOUNT_CREATED_EMAIL_FAILED";
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizePhone(string phone)
    {
        var normalized = phone.Trim();

        if (normalized.StartsWith("+84", StringComparison.Ordinal))
        {
            normalized = "0" + normalized[3..];
        }

        return normalized;
    }

    private static string NormalizeRole(string role)
    {
        return Enum.Parse<RoleEnum>(
            role.Trim(),
            ignoreCase: true)
            .ToString();
    }

    private async Task<(string Field, string Message)?> FindConflictAsync(
        string username,
        string email,
        string phone,
        CancellationToken cancellationToken)
    {
        var internationalPhone = ToInternationalPhone(phone);

        if (await _userOperations.ExistsByUsernameAsync(username, cancellationToken))
        {
            return ("username", GetConflictMessage("username"));
        }

        if (await _userOperations.ExistsByEmailAsync(email, cancellationToken))
        {
            return ("email", GetConflictMessage("email"));
        }

        if (await _userOperations.ExistsByPhoneAsync(phone, internationalPhone, cancellationToken))
        {
            return ("phone", GetConflictMessage("phone"));
        }

        return null;
    }

    private static string ToInternationalPhone(string phone)
    {
        return "+84" + phone[1..];
    }

    private async Task<Role?> FindRoleAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        return await _userOperations.GetRoleByNameAsync(roleName, cancellationToken);
    }

    private static UserProfile CreateUserProfile(
        CreateUserCommand request,
        User user)
    {
        return new UserProfile
        {
            UserId = user.Id,
            User = user,
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Address = NormalizeAddress(request.Address)
        };
    }

    private static string? NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        return address.Trim();
    }

    private static IActionResult CreatedResponse(
        User user,
        UserProfile profile,
        string roleName)
    {
        var response = new CreateUserResponse
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Phone = user.Phone ?? string.Empty,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Address = profile.Address,
            Role = roleName,
            Status = user.Status ?? string.Empty,
            CreatedAt = user.CreatedAt ?? DateTime.UtcNow
        };

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    private static string GetConflictMessage(string field)
    {
        return field switch
        {
            "username" => "Tên đăng nhập đã được sử dụng.",
            "email" => "Email đã được sử dụng.",
            "phone" => "Số điện thoại đã được sử dụng.",
            "role" => "Vai trò chưa được cấu hình hoặc đã ngừng sử dụng.",
            _ => "Dữ liệu đã tồn tại."
        };
    }

    private static IActionResult Conflict(
        string field,
        string message)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Không thể tạo tài khoản."
        };

        problem.Extensions["errors"] =
            new Dictionary<string, string[]>
            {
                [field] = [message]
            };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" }
        };
    }
}
