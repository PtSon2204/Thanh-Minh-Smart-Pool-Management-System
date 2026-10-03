using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using FluentValidation;
using System.Text.Json;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, IActionResult>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterHandler> _logger;

    private readonly IValidator<RegisterCommand> _validator;

    public RegisterHandler(
        IRepository<User> users,
        IRepository<UserProfile> profiles,
        IRepository<Role> roles,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<RegisterCommand> validator,
        ILogger<RegisterHandler> logger)
    {
        _users = users;
        _profiles = profiles;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await RegisterAsync(request, cancellationToken);
            return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }
        catch (UniqueFieldConflictException exception)
        {
            return CreateConflictResult(exception.Field);
        }
        catch (CustomerRoleNotConfiguredException)
        {
            return CreateProblemResult(
                StatusCodes.Status500InternalServerError,
                "Cấu hình đăng ký chưa sẵn sàng.",
                "Role CUSTOMER chưa tồn tại trong hệ thống.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateUnexpectedErrorResult(exception);
        }
    }

    private async Task<RegisterResponse> RegisterAsync(RegisterCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var inputPhone = request.Phone.Trim();
        var phone = inputPhone.StartsWith("+84", StringComparison.Ordinal)
            ? "0" + inputPhone[3..]
            : inputPhone;
        var fullName = request.FullName.Trim();
        var address = request.Address?.Trim();
        if (address == string.Empty)
            address = null;

        // SQL unique indexes also cover soft-deleted users.
        if (await _users.ExistsAsync(
                user => user.Username != null && user.Username.ToLower() == username,
                cancellationToken))
            throw new UniqueFieldConflictException(nameof(RegisterCommand.Username));

        if (await _users.ExistsAsync(
                user => user.Email != null && user.Email.ToLower() == email,
                cancellationToken))
            throw new UniqueFieldConflictException(nameof(RegisterCommand.Email));

        var internationalPhone = phone.StartsWith('0') ? "+84" + phone[1..] : inputPhone;
        if (await _users.ExistsAsync(
                user => user.Phone == phone || user.Phone == internationalPhone,
                cancellationToken))
            throw new UniqueFieldConflictException(nameof(RegisterCommand.Phone));

        var customerRole = await _roles.FirstOrDefaultAsync(
            role => role.Name.ToUpper() == nameof(RoleEnum.CUSTOMER) && role.IsDeleted != true,
            cancellationToken);
        if (customerRole is null)
            throw new CustomerRoleNotConfiguredException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            Phone = phone,
            RoleId = customerRole.Id,
            Status = "Active",
            IsDeleted = false
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var profile = new UserProfile
        {
            UserId = user.Id,
            User = user,
            FullName = fullName,
            DateOfBirth = request.DateOfBirth,
            Address = address
        };
        user.UserProfile = profile;

        await _users.AddAsync(user, cancellationToken);
        await _profiles.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterResponse
        {
            Id = user.Id,
            Username = username,
            Email = email,
            Phone = phone,
            FullName = fullName,
            Role = RoleEnum.CUSTOMER,
            Status = user.Status,
            CreatedAt = user.CreatedAt ?? DateTime.UtcNow
        };
    }

    private static IActionResult CreateConflictResult(string field)
    {
        var fieldName = JsonNamingPolicy.CamelCase.ConvertName(field);
        var message = fieldName switch
        {
            "username" => "Tên đăng nhập đã được sử dụng.",
            "email" => "Email đã được sử dụng.",
            "phone" => "Số điện thoại đã được sử dụng.",
            _ => "Thông tin đăng ký đã được sử dụng."
        };

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Thông tin đăng ký bị trùng."
        };
        problem.Extensions["errors"] = new Dictionary<string, string[]>
        {
            [fieldName] = [message]
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" }
        };
    }

    private IActionResult CreateUnexpectedErrorResult(Exception exception)
    {
        _logger.LogError(exception, "Đăng ký tài khoản thất bại do lỗi không mong đợi.");
        return CreateProblemResult(
            StatusCodes.Status500InternalServerError,
            "Không thể đăng ký tài khoản.",
            "Hệ thống gặp lỗi khi xử lý đăng ký.");
    }

    private static IActionResult CreateProblemResult(int status, string title, string detail)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    public sealed class CustomerRoleNotConfiguredException : Exception
    {
        public CustomerRoleNotConfiguredException()
            : base("Role CUSTOMER chưa tồn tại trong hệ thống.")
        {
        }
    }


    public sealed class UniqueFieldConflictException : Exception
    {
        public string Field { get; }

        public UniqueFieldConflictException(string field, Exception? innerException = null)
            : base($"The field '{field}' already exists.", innerException)
        {
            Field = field;
        }
    }

}
