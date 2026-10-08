using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Commands.CreateUser;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, IActionResult>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CreateUserCommand> _validator;

    public CreateUserHandler(
        IRepository<User> users,
        IRepository<UserProfile> profiles,
        IRepository<Role> roles,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<CreateUserCommand> validator)
    {
        _users = users;
        _profiles = profiles;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _validator = validator;
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

        var user = CreateUser(
            request,
            username,
            email,
            phone,
            role.Id);

        var profile = CreateUserProfile(request, user);

        user.UserProfile = profile;

        await _users.AddAsync(user, cancellationToken);
        await _profiles.AddAsync(profile, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueUserFieldException exception)
        {
            return Conflict(
                exception.Field,
                GetConflictMessage(exception.Field));
        }

        return CreatedResponse(
            user,
            profile,
            roleName);
    }

    
    private User CreateUser(
        CreateUserCommand request,
        string username,
        string email,
        string phone,
        Guid roleId)
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
            request.Password);

        return user;
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

        if (await _users.ExistsAsync(
                user =>
                    user.Username != null &&
                    user.Username.ToLower() == username,
                cancellationToken))
        {
            return ("username", GetConflictMessage("username"));
        }

        if (await _users.ExistsAsync(
                user =>
                    user.Email != null &&
                    user.Email.ToLower() == email,
                cancellationToken))
        {
            return ("email", GetConflictMessage("email"));
        }

        if (await _users.ExistsAsync(
                user =>
                    user.Phone == phone ||
                    user.Phone == internationalPhone,
                cancellationToken))
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
        return await _roles.FirstOrDefaultAsync(
            role =>
                role.Name != null &&
                role.Name.ToUpper() == roleName.ToUpper() &&
                role.IsDeleted != true,
            cancellationToken);
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
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Address = profile.Address,
            Role = roleName,
            Status = user.Status,
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
