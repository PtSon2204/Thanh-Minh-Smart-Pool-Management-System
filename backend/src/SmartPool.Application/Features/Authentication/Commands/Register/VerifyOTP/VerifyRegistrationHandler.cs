using System.Globalization;
using System.Text.Json;
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

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class VerifyRegistrationHandler : IRequestHandler<VerifyRegistrationCommand, IActionResult>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistrationOtpStore _otpStore;
    private readonly RegistrationOtpCodeService _otpCodes;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<VerifyRegistrationCommand> _validator;
    private readonly ILogger<VerifyRegistrationHandler> _logger;

    public VerifyRegistrationHandler(
        IRepository<User> users,
        IRepository<UserProfile> profiles,
        IRepository<Role> roles,
        IUnitOfWork unitOfWork,
        IRegistrationOtpStore otpStore,
        RegistrationOtpCodeService otpCodes,
        IHttpContextAccessor httpContextAccessor,
        IValidator<VerifyRegistrationCommand> validator,
        ILogger<VerifyRegistrationHandler> logger)
    {
        _users = users;
        _profiles = profiles;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _otpStore = otpStore;
        _otpCodes = otpCodes;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(VerifyRegistrationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return new BadRequestObjectResult(new ValidationProblemDetails(errors));
        }

        RegistrationOtpStoreResult challenge;
        try
        {
            var digest = _otpCodes.ComputeDigest(request.RegistrationId, request.Otp);
            challenge = await _otpStore.TryBeginVerificationAsync(request.RegistrationId, digest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kiểm tra OTP đăng ký: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }

        if (challenge.Status != RegistrationOtpStoreStatus.Success)
            return MapChallengeResult(challenge);
        if (challenge.Registration is null || string.IsNullOrEmpty(challenge.LeaseId))
        {
            if (!string.IsNullOrEmpty(challenge.LeaseId))
                await ReleaseQuietlyAsync(request.RegistrationId, challenge.LeaseId);
            return StoreUnavailable();
        }

        var registration = challenge.Registration;
        var leaseId = challenge.LeaseId;
        var saved = false;
        User? user = null;
        try
        {
            var conflict = await RegisterHandler.FindConflictAsync(_users, registration.Username,
                registration.Email, registration.Phone, cancellationToken);
            if (conflict is not null)
                return RegisterHandler.CreateConflictResult(conflict);

            var customerRole = await _roles.FirstOrDefaultAsync(
                role => role.Name.ToUpper() == nameof(RoleEnum.CUSTOMER) && role.IsDeleted != true,
                cancellationToken);
            if (customerRole is null)
                return RegisterHandler.CreateProblemResult(StatusCodes.Status500InternalServerError,
                    "Cấu hình đăng ký chưa sẵn sàng.", "Role CUSTOMER chưa tồn tại trong hệ thống.",
                    "CUSTOMER_ROLE_NOT_CONFIGURED");

            user = new User
            {
                Id = Guid.NewGuid(),
                Username = registration.Username,
                Email = registration.Email,
                Phone = registration.Phone,
                PasswordHash = registration.PasswordHash,
                RoleId = customerRole.Id,
                Status = "Active",
                IsDeleted = false
            };
            var profile = new UserProfile
            {
                UserId = user.Id,
                User = user,
                FullName = registration.FullName,
                DateOfBirth = registration.DateOfBirth,
                Address = registration.Address
            };
            user.UserProfile = profile;

            await _users.AddAsync(user, cancellationToken);
            await _profiles.AddAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            saved = true;
        }
        catch (UniqueUserFieldException exception)
        {
            return RegisterHandler.CreateConflictResult(exception.Field);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("Không thể lưu tài khoản đã xác minh: {ErrorType}.", exception.GetType().Name);
            return RegisterHandler.CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể hoàn tất đăng ký.", "Hệ thống chưa thể lưu tài khoản. Vui lòng thử lại.",
                "REGISTRATION_SAVE_FAILED");
        }
        finally
        {
            if (!saved) await ReleaseQuietlyAsync(request.RegistrationId, leaseId);
        }

        // A database commit is final even if Redis cleanup fails. Unique indexes prevent a second account.
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            if (!await _otpStore.CompleteVerificationAsync(request.RegistrationId, leaseId, timeout.Token))
                _logger.LogWarning("Không thể đánh dấu OTP đăng ký đã dùng sau khi lưu tài khoản.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể hoàn tất trạng thái OTP trong Redis: {ErrorType}.",
                exception.GetType().Name);
        }

        return new ObjectResult(new RegisterResponse
        {
            Id = user!.Id,
            Username = registration.Username,
            Email = registration.Email,
            Phone = registration.Phone,
            FullName = registration.FullName,
            Role = RoleEnum.CUSTOMER,
            Status = user.Status ?? "Active",
            CreatedAt = user.CreatedAt ?? DateTime.UtcNow
        }) { StatusCode = StatusCodes.Status201Created };
    }

    private IActionResult MapChallengeResult(RegistrationOtpStoreResult result)
    {
        switch (result.Status)
        {
            case RegistrationOtpStoreStatus.InvalidOtp:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status400BadRequest,
                    "OTP không hợp lệ.", "Vui lòng kiểm tra mã và thử lại.", "INVALID_OTP");
            case RegistrationOtpStoreStatus.OtpExpired:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status410Gone,
                    "OTP đã hết hạn.", "Vui lòng yêu cầu gửi lại mã.", "OTP_EXPIRED");
            case RegistrationOtpStoreStatus.Unavailable:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status410Gone,
                    "Phiên đăng ký không còn hiệu lực.", "Vui lòng bắt đầu đăng ký lại.",
                    "REGISTRATION_UNAVAILABLE");
            case RegistrationOtpStoreStatus.AttemptsExceeded:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status429TooManyRequests,
                    "Đã nhập OTP sai quá số lần cho phép.", "Vui lòng bắt đầu đăng ký lại sau khi phiên hết hạn.",
                    "VERIFY_ATTEMPTS_EXCEEDED");
            case RegistrationOtpStoreStatus.AlreadyVerified:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status409Conflict,
                    "Phiên đăng ký đã được xác minh.", "Tài khoản đã được tạo; vui lòng đăng nhập.",
                    "ALREADY_VERIFIED");
            case RegistrationOtpStoreStatus.Busy:
                if (result.RetryAfter is { } delay && _httpContextAccessor.HttpContext is { } context)
                    context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
                return RegisterHandler.CreateProblemResult(StatusCodes.Status409Conflict,
                    "Phiên đăng ký đang được xác minh.", "Vui lòng thử lại sau.",
                    "VERIFICATION_IN_PROGRESS");
            default:
                return StoreUnavailable();
        }
    }

    private async Task ReleaseQuietlyAsync(Guid registrationId, string leaseId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _otpStore.ReleaseVerificationAsync(registrationId, leaseId, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể mở khóa phiên xác minh đăng ký: {ErrorType}.",
                exception.GetType().Name);
        }
    }

    private static IActionResult StoreUnavailable() => RegisterHandler.CreateProblemResult(
        StatusCodes.Status503ServiceUnavailable, "Không thể xác minh OTP.",
        "Bộ lưu trữ mã xác minh chưa sẵn sàng.", "OTP_STORE_UNAVAILABLE");
}
