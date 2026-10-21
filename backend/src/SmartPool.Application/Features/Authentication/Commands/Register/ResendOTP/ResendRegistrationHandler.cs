using SmartPool.Application.Features.Authentication.Models;
using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Authentication.Commands.Register.ResendOTP;

public sealed class ResendRegistrationHandler : IRequestHandler<ResendRegistrationCommand, IActionResult>
{
    private readonly IRegistrationOtpStore _otpStore;
    private readonly RegistrationOtpCodeService _otpCodes;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<ResendRegistrationCommand> _validator;
    private readonly ILogger<ResendRegistrationHandler> _logger;

    public ResendRegistrationHandler(
        IRegistrationOtpStore otpStore,
        RegistrationOtpCodeService otpCodes,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        IValidator<ResendRegistrationCommand> validator,
        ILogger<ResendRegistrationHandler> logger)
    {
        _otpStore = otpStore;
        _otpCodes = otpCodes;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(ResendRegistrationCommand request, CancellationToken cancellationToken)
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

        RegistrationOtpStoreResult started;
        try
        {
            started = await _otpStore.TryBeginResendAsync(request.RegistrationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể bắt đầu gửi lại OTP: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }

        if (started.Status != RegistrationOtpStoreStatus.Success)
            return MapStoreResult(started);
        if (string.IsNullOrWhiteSpace(started.LeaseId) || string.IsNullOrWhiteSpace(started.Email))
        {
            if (!string.IsNullOrWhiteSpace(started.LeaseId))
                await CancelQuietlyAsync(request.RegistrationId, started.LeaseId);
            return StoreUnavailable();
        }

        var leaseId = started.LeaseId;
        var otp = _otpCodes.GenerateCode();
        var digest = _otpCodes.ComputeDigest(request.RegistrationId, otp);
        try
        {
            await _emailService.SendAsync(started.Email, RegisterHandler.OtpEmailSubject,
                RegisterHandler.BuildOtpEmailBody(otp), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelQuietlyAsync(request.RegistrationId, leaseId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Gửi lại email OTP thất bại: {ErrorType}.", exception.GetType().Name);
            await CancelQuietlyAsync(request.RegistrationId, leaseId);
            return RegisterHandler.CreateProblemResult(StatusCodes.Status503ServiceUnavailable,
                "Không thể gửi lại mã xác minh.", "Vui lòng thử lại sau.", "EMAIL_SEND_FAILED");
        }

        RegistrationOtpStoreResult completed;
        try
        {
            // Finish after SMTP accepts the message, even if the HTTP request is cancelled.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            completed = await _otpStore.CompleteResendAsync(request.RegistrationId, leaseId, digest, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể phát hành OTP gửi lại: {ErrorType}.", exception.GetType().Name);
            await CancelQuietlyAsync(request.RegistrationId, leaseId);
            return StoreUnavailable();
        }

        if (completed.Status == RegistrationOtpStoreStatus.Success &&
            completed.OtpExpiresAtUtc is { } otpExpires &&
            completed.RegistrationExpiresAtUtc is { } registrationExpires &&
            completed.ResendAvailableAtUtc is { } resendAvailable)
            return new ObjectResult(new ResendRegistrationResponse(otpExpires, registrationExpires, resendAvailable))
            {
                StatusCode = StatusCodes.Status202Accepted
            };

        await CancelQuietlyAsync(request.RegistrationId, leaseId);
        return MapStoreResult(completed);
    }

    private IActionResult MapStoreResult(RegistrationOtpStoreResult result)
    {
        switch (result.Status)
        {
            case RegistrationOtpStoreStatus.Unavailable:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status410Gone,
                    "Phiên đăng ký không còn hiệu lực.", "Vui lòng bắt đầu đăng ký lại.",
                    "REGISTRATION_UNAVAILABLE");
            case RegistrationOtpStoreStatus.ResendCooldown:
                SetRetryAfter(result.RetryAfter);
                return RegisterHandler.CreateProblemResult(StatusCodes.Status429TooManyRequests,
                    "Chưa thể gửi lại mã.", "Vui lòng đợi hết thời gian chờ.", "RESEND_COOLDOWN");
            case RegistrationOtpStoreStatus.ResendLimitReached:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status429TooManyRequests,
                    "Đã gửi lại mã quá số lần cho phép.", "Vui lòng bắt đầu đăng ký lại sau khi phiên hết hạn.",
                    "RESEND_LIMIT_REACHED");
            case RegistrationOtpStoreStatus.AttemptsExceeded:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status429TooManyRequests,
                    "Đã nhập OTP sai quá số lần cho phép.", "Vui lòng bắt đầu đăng ký lại sau khi phiên hết hạn.",
                    "VERIFY_ATTEMPTS_EXCEEDED");
            case RegistrationOtpStoreStatus.AlreadyVerified:
                return RegisterHandler.CreateProblemResult(StatusCodes.Status409Conflict,
                    "Phiên đăng ký đã được xác minh.", "Tài khoản đã được tạo; vui lòng đăng nhập.",
                    "ALREADY_VERIFIED");
            case RegistrationOtpStoreStatus.Busy:
                SetRetryAfter(result.RetryAfter);
                return RegisterHandler.CreateProblemResult(StatusCodes.Status409Conflict,
                    "Đang xử lý phiên đăng ký.", "Vui lòng thử lại sau.", "RESEND_IN_PROGRESS");
            default:
                return StoreUnavailable();
        }
    }

    private void SetRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter is { } duration && _httpContextAccessor.HttpContext is { } context)
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
    }

    private async Task CancelQuietlyAsync(Guid registrationId, string leaseId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _otpStore.CancelResendAsync(registrationId, leaseId, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể hủy lượt gửi lại OTP: {ErrorType}.", exception.GetType().Name);
            // The lease expires in Redis even if cleanup cannot reach it.
        }
    }

    private static IActionResult StoreUnavailable() => RegisterHandler.CreateProblemResult(
        StatusCodes.Status503ServiceUnavailable, "Không thể gửi lại mã xác minh.",
        "Bộ lưu trữ mã xác minh chưa sẵn sàng.", "OTP_STORE_UNAVAILABLE");
}
