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
using System.Globalization;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, IActionResult>
{
    internal const string OtpEmailSubject = "[Thanh Minh Smart Pool] Mã xác minh đăng ký";

    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRegistrationOtpStore _otpStore;
    private readonly RegistrationOtpCodeService _otpCodes;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RegisterHandler> _logger;

    private readonly IValidator<RegisterCommand> _validator;

    public RegisterHandler(
        IRepository<User> users,
        IRepository<Role> roles,
        IPasswordHasher passwordHasher,
        IRegistrationOtpStore otpStore,
        RegistrationOtpCodeService otpCodes,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        IValidator<RegisterCommand> validator,
        ILogger<RegisterHandler> logger)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _otpStore = otpStore;
        _otpCodes = otpCodes;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await StartRegistrationAsync(request, cancellationToken);
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
                "Role CUSTOMER chưa tồn tại trong hệ thống.",
                "CUSTOMER_ROLE_NOT_CONFIGURED");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("Khởi tạo đăng ký thất bại do lỗi {ErrorType}.", exception.GetType().Name);
            return CreateProblemResult(StatusCodes.Status500InternalServerError,
                "Không thể đăng ký tài khoản.", "Hệ thống gặp lỗi khi xử lý đăng ký.");
        }
    }

    private async Task<IActionResult> StartRegistrationAsync(RegisterCommand request, CancellationToken cancellationToken)
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

        var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (remoteIp is null)
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể khởi tạo đăng ký.", "Không xác định được địa chỉ kết nối.",
                "CLIENT_IP_UNAVAILABLE");
        var ipAddress = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4().ToString() : remoteIp.ToString();

        RegistrationOtpStoreResult quota;
        try
        {
            quota = await _otpStore.TryConsumeStartQuotaAsync(email, ipAddress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kiểm tra giới hạn đăng ký: {ErrorType}.", exception.GetType().Name);
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể khởi tạo đăng ký.", "Bộ lưu trữ mã xác minh chưa sẵn sàng.",
                "OTP_STORE_UNAVAILABLE");
        }
        if (quota.Status == RegistrationOtpStoreStatus.RateLimited)
        {
            SetRetryAfter(quota.RetryAfter);
            return CreateProblemResult(StatusCodes.Status429TooManyRequests,
                "Đã gửi quá nhiều yêu cầu đăng ký.", "Vui lòng thử lại sau.", "START_RATE_LIMITED");
        }
        if (quota.Status != RegistrationOtpStoreStatus.Success)
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể khởi tạo đăng ký.", "Bộ lưu trữ mã xác minh chưa sẵn sàng.",
                "OTP_STORE_UNAVAILABLE");

        var conflict = await FindConflictAsync(_users, username, email, phone, cancellationToken);
        if (conflict is not null) throw new UniqueFieldConflictException(conflict);

        var customerRole = await _roles.FirstOrDefaultAsync(
            role => role.Name.ToUpper() == nameof(RoleEnum.CUSTOMER) && role.IsDeleted != true,
            cancellationToken);
        if (customerRole is null)
            throw new CustomerRoleNotConfiguredException();

        var registrationId = Guid.NewGuid();
        var otp = _otpCodes.GenerateCode();
        var otpDigest = _otpCodes.ComputeDigest(registrationId, otp);
        // The BCrypt hasher accepts a User argument; this transient object is never persisted.
        var passwordHash = _passwordHasher.HashPassword(new User { Id = registrationId }, request.Password);
        var pending = new PendingRegistration(registrationId, username, email, phone, passwordHash,
            fullName, request.DateOfBirth, address, otpDigest);

        RegistrationOtpStoreResult stored;
        try
        {
            stored = await _otpStore.TryStartAsync(pending, ipAddress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể lưu phiên đăng ký tạm: {ErrorType}.", exception.GetType().Name);
            await CancelPendingQuietlyAsync(registrationId, email);
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể khởi tạo đăng ký.", "Bộ lưu trữ mã xác minh chưa sẵn sàng.",
                "OTP_STORE_UNAVAILABLE");
        }

        if (stored.Status == RegistrationOtpStoreStatus.PendingRegistration)
            return CreateProblemResult(StatusCodes.Status409Conflict,
                "Email đang chờ xác minh.", "Vui lòng xác minh phiên đăng ký hiện có hoặc thử lại sau khi hết hạn.",
                "PENDING_REGISTRATION");
        if (stored.Status != RegistrationOtpStoreStatus.Success ||
            stored.OtpExpiresAtUtc is null || stored.RegistrationExpiresAtUtc is null ||
            stored.ResendAvailableAtUtc is null)
        {
            await CancelPendingQuietlyAsync(registrationId, email);
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể khởi tạo đăng ký.", "Bộ lưu trữ mã xác minh chưa sẵn sàng.",
                "OTP_STORE_UNAVAILABLE");
        }

        try
        {
            await _emailService.SendAsync(email, OtpEmailSubject,
                BuildOtpEmailBody(otp, stored.OtpExpiresAtUtc.Value),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelPendingQuietlyAsync(registrationId, email);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Gửi email OTP đăng ký thất bại: {ErrorType}.", exception.GetType().Name);
            await CancelPendingQuietlyAsync(registrationId, email);
            return CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể gửi mã xác minh.", "Vui lòng thử đăng ký lại sau.", "EMAIL_SEND_FAILED");
        }

        return new ObjectResult(new RegisterPendingResponse(registrationId,
            stored.OtpExpiresAtUtc.Value, stored.RegistrationExpiresAtUtc.Value,
            stored.ResendAvailableAtUtc.Value))
        {
            StatusCode = StatusCodes.Status202Accepted
        };
    }

    private async Task CancelPendingQuietlyAsync(Guid registrationId, string email)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _otpStore.CancelStartAsync(registrationId, email, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể hủy phiên đăng ký tạm: {ErrorType}.", exception.GetType().Name);
            // The Redis session still expires by TTL if cleanup cannot reach Redis.
        }
    }

    private void SetRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter is { } duration && _httpContextAccessor.HttpContext is { } context)
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
    }

    internal static string BuildOtpEmailBody(string otp, DateTimeOffset? expiresAtUtc = null)
    {
        var validity = expiresAtUtc is { } expiry
            ? $"Mã có hiệu lực khoảng {Math.Max(1, (int)Math.Ceiling((expiry - DateTimeOffset.UtcNow).TotalMinutes))} phút. "
            : string.Empty;
        return $"<p>Mã xác minh đăng ký tài khoản của bạn là <strong>{otp}</strong>.</p>" +
            $"<p>{validity}Nếu bạn không yêu cầu đăng ký, hãy bỏ qua email này.</p>";
    }

    // SQL unique indexes also cover soft-deleted users. Reused before OTP issue and DB creation.
    internal static async Task<string?> FindConflictAsync(IRepository<User> users, string username,
        string email, string phone, CancellationToken cancellationToken)
    {
        if (await users.ExistsAsync(
                user => user.Username != null && user.Username.ToLower() == username, cancellationToken))
            return "username";
        if (await users.ExistsAsync(
                user => user.Email != null && user.Email.ToLower() == email, cancellationToken))
            return "email";
        var internationalPhone = "+84" + phone[1..];
        if (await users.ExistsAsync(
                user => user.Phone == phone || user.Phone == internationalPhone, cancellationToken))
            return "phone";
        return null;
    }

    internal static IActionResult CreateConflictResult(string field)
    {
        var fieldName = JsonNamingPolicy.CamelCase.ConvertName(field);
        var (message, code) = fieldName switch
        {
            "username" => ("Tên đăng nhập đã được sử dụng.", "USERNAME_TAKEN"),
            "email" => ("Email đã được sử dụng.", "EMAIL_TAKEN"),
            "phone" => ("Số điện thoại đã được sử dụng.", "PHONE_TAKEN"),
            _ => ("Thông tin đăng ký đã được sử dụng.", "REGISTRATION_CONFLICT")
        };

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Thông tin đăng ký bị trùng."
        };
        problem.Extensions["code"] = code;
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

    internal static IActionResult CreateProblemResult(int status, string title, string detail, string? code = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        if (code is not null) problem.Extensions["code"] = code;
        return new ObjectResult(problem)
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
