using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class ResendPasswordResetHandler : IRequestHandler<ResendPasswordResetCommand, IActionResult>
{
    private static readonly TimeSpan EmailTimeout = TimeSpan.FromSeconds(30);

    private readonly IPasswordResetStore _store;
    private readonly PasswordResetCodeService _codes;
    private readonly IEmailService _email;
    private readonly IHttpContextAccessor _http;
    private readonly IValidator<ResendPasswordResetCommand> _validator;
    private readonly ILogger<ResendPasswordResetHandler> _logger;

    public ResendPasswordResetHandler(
        IPasswordResetStore store,
        PasswordResetCodeService codes,
        IEmailService email,
        IHttpContextAccessor http,
        IValidator<ResendPasswordResetCommand> validator,
        ILogger<ResendPasswordResetHandler> logger)
    {
        _store = store;
        _codes = codes;
        _email = email;
        _http = http;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(ResendPasswordResetCommand request, CancellationToken cancellationToken)
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

        PasswordResetStoreResult started;
        try
        {
            started = await _store.TryBeginResendAsync(request.RecoveryId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể bắt đầu gửi lại mã khôi phục: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }

        if (started.Status != PasswordResetStoreStatus.Success)
            return MapStoreResult(started);
        if (string.IsNullOrWhiteSpace(started.LeaseId) ||
            started.OtpExpiresAtUtc is null || started.RecoveryExpiresAtUtc is null ||
            started.ResendAvailableAtUtc is null)
        {
            if (!string.IsNullOrWhiteSpace(started.LeaseId))
                await CancelQuietlyAsync(request.RecoveryId, started.LeaseId);
            return StoreUnavailable();
        }

        var lease = started.LeaseId;
        if (started.Email is null)
            return Accepted(await FinishWithoutNewCodeAsync(request.RecoveryId, lease, started));

        var code = _codes.GenerateCode();
        var digest = _codes.ComputeOtpDigest(request.RecoveryId, code);
        try
        {
            // The Redis lease lasts 60 seconds; bound SMTP work below that window.
            using var emailTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            emailTimeout.CancelAfter(EmailTimeout);
            await _email.SendAsync(started.Email, RequestPasswordResetHandler.EmailSubject,
                RequestPasswordResetHandler.BuildEmailBody(code), emailTimeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FinishWithoutNewCodeAsync(request.RecoveryId, lease, started);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Gửi lại mã khôi phục thất bại: {ErrorType}.", exception.GetType().Name);
            return Accepted(await FinishWithoutNewCodeAsync(request.RecoveryId, lease, started));
        }

        try
        {
            // Commit the new digest only after SMTP accepted the message.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var completed = await _store.CompleteResendAsync(request.RecoveryId, lease, digest, timeout.Token);
            if (HasTimings(completed))
                return Accepted(completed);
            _logger.LogWarning("Không thể kích hoạt mã khôi phục gửi lại: {Status}.", completed.Status);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kích hoạt mã khôi phục gửi lại: {ErrorType}.", exception.GetType().Name);
        }

        // The previous OTP stays valid if the new digest could not be committed.
        return Accepted(await FinishWithoutNewCodeAsync(request.RecoveryId, lease, started));
    }

    private async Task<PasswordResetStoreResult> FinishWithoutNewCodeAsync(
        Guid recoveryId, string lease, PasswordResetStoreResult fallback)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var finished = await _store.FinishResendWithoutNewCodeAsync(recoveryId, lease, timeout.Token);
            if (HasTimings(finished)) return finished;
            _logger.LogWarning("Không thể kết thúc lượt gửi lại mã khôi phục: {Status}.", finished.Status);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kết thúc lượt gửi lại mã khôi phục: {ErrorType}.", exception.GetType().Name);
        }

        await CancelQuietlyAsync(recoveryId, lease);
        return fallback;
    }

    private async Task CancelQuietlyAsync(Guid recoveryId, string lease)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _store.CancelResendAsync(recoveryId, lease, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể hủy lease gửi lại mã khôi phục: {ErrorType}.", exception.GetType().Name);
        }
    }

    private IActionResult MapStoreResult(PasswordResetStoreResult result)
    {
        switch (result.Status)
        {
            case PasswordResetStoreStatus.Unavailable:
                return Problem(StatusCodes.Status410Gone, "RECOVERY_UNAVAILABLE",
                    "Phiên khôi phục đã hết hạn.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.ResendCooldown:
                SetRetryAfter(result.RetryAfter);
                return Problem(StatusCodes.Status429TooManyRequests, "RESEND_COOLDOWN",
                    "Chưa thể gửi lại mã.", "Vui lòng đợi hết thời gian chờ.");
            case PasswordResetStoreStatus.ResendLimitReached:
                return Problem(StatusCodes.Status429TooManyRequests, "RESEND_LIMIT_REACHED",
                    "Đã gửi lại mã quá số lần cho phép.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.AttemptsExceeded:
                return Problem(StatusCodes.Status429TooManyRequests, "VERIFY_ATTEMPTS_EXCEEDED",
                    "Đã nhập mã sai quá số lần cho phép.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.Busy:
                SetRetryAfter(result.RetryAfter);
                return Problem(StatusCodes.Status409Conflict, "RECOVERY_IN_PROGRESS",
                    "Phiên khôi phục đang được xử lý.", "Vui lòng thử lại sau.");
            case PasswordResetStoreStatus.AlreadyVerified:
                return Problem(StatusCodes.Status409Conflict, "ALREADY_VERIFIED",
                    "Phiên khôi phục đã được xác minh.", "Vui lòng tiếp tục đặt mật khẩu mới.");
            default:
                return StoreUnavailable();
        }
    }

    private void SetRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter is { } duration && _http.HttpContext is { } context)
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
    }

    private static bool HasTimings(PasswordResetStoreResult result) =>
        result.Status == PasswordResetStoreStatus.Success && result.OtpExpiresAtUtc is not null &&
        result.RecoveryExpiresAtUtc is not null && result.ResendAvailableAtUtc is not null;

    private static IActionResult Accepted(PasswordResetStoreResult result) =>
        new ObjectResult(new ResendPasswordResetResponse(result.OtpExpiresAtUtc!.Value,
            result.RecoveryExpiresAtUtc!.Value, result.ResendAvailableAtUtc!.Value))
        { StatusCode = StatusCodes.Status202Accepted };

    private static IActionResult Problem(int status, string code, string title, string detail) =>
        RegisterHandler.CreateProblemResult(status, title, detail, code);

    private static IActionResult StoreUnavailable() => Problem(StatusCodes.Status503ServiceUnavailable,
        "OTP_STORE_UNAVAILABLE", "Không thể gửi lại mã khôi phục.",
        "Bộ lưu trữ mã chưa sẵn sàng. Vui lòng thử lại sau.");
}
