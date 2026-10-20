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

public sealed class VerifyPasswordResetHandler : IRequestHandler<VerifyPasswordResetCommand, IActionResult>
{
    private readonly IPasswordResetStore _store;
    private readonly PasswordResetCodeService _codes;
    private readonly IHttpContextAccessor _http;
    private readonly IValidator<VerifyPasswordResetCommand> _validator;
    private readonly ILogger<VerifyPasswordResetHandler> _logger;

    public VerifyPasswordResetHandler(
        IPasswordResetStore store,
        PasswordResetCodeService codes,
        IHttpContextAccessor http,
        IValidator<VerifyPasswordResetCommand> validator,
        ILogger<VerifyPasswordResetHandler> logger)
    {
        _store = store;
        _codes = codes;
        _http = http;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(VerifyPasswordResetCommand request, CancellationToken cancellationToken)
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

        PasswordResetStoreResult challenge;
        try
        {
            var digest = _codes.ComputeOtpDigest(request.RecoveryId, request.Otp);
            challenge = await _store.TryBeginVerificationAsync(request.RecoveryId, digest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kiểm tra OTP khôi phục: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }

        if (challenge.Status != PasswordResetStoreStatus.Success)
            return MapChallengeResult(challenge);
        if (string.IsNullOrWhiteSpace(challenge.LeaseId) ||
            challenge.Identity is null || challenge.Identity.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(challenge.Identity.Email) ||
            string.IsNullOrWhiteSpace(challenge.Identity.PasswordFingerprint))
        {
            if (!string.IsNullOrWhiteSpace(challenge.LeaseId))
                await ReleaseQuietlyAsync(request.RecoveryId, challenge.LeaseId);
            return StoreUnavailable();
        }

        var lease = challenge.LeaseId;
        var committed = false;
        try
        {
            var token = _codes.GenerateResetToken();
            var tokenDigest = _codes.ComputeTokenDigest(request.RecoveryId, token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var completed = await _store.CompleteVerificationAsync(
                request.RecoveryId, lease, tokenDigest, timeout.Token);
            if (completed.Status == PasswordResetStoreStatus.Success &&
                completed.ResetTokenExpiresAtUtc is { } expiry)
            {
                committed = true;
                return new OkObjectResult(new VerifyPasswordResetResponse(token, expiry));
            }

            _logger.LogWarning("Không thể cấp quyền đặt lại mật khẩu: {Status}.", completed.Status);
            return completed.Status == PasswordResetStoreStatus.Unavailable
                ? Problem(StatusCodes.Status410Gone, "RECOVERY_UNAVAILABLE",
                    "Phiên khôi phục không còn hiệu lực.", "Vui lòng bắt đầu lại quy trình khôi phục.")
                : StoreUnavailable();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể cấp quyền đặt lại mật khẩu: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }
        finally
        {
            if (!committed) await ReleaseQuietlyAsync(request.RecoveryId, lease);
        }
    }

    private IActionResult MapChallengeResult(PasswordResetStoreResult result)
    {
        switch (result.Status)
        {
            case PasswordResetStoreStatus.InvalidOtp:
                return Problem(StatusCodes.Status400BadRequest, "INVALID_OTP",
                    "OTP không hợp lệ.", "Vui lòng kiểm tra mã và thử lại.");
            case PasswordResetStoreStatus.OtpExpired:
                return Problem(StatusCodes.Status410Gone, "OTP_EXPIRED",
                    "OTP đã hết hạn.", "Vui lòng yêu cầu gửi lại mã.");
            case PasswordResetStoreStatus.Unavailable:
                return Problem(StatusCodes.Status410Gone, "RECOVERY_UNAVAILABLE",
                    "Phiên khôi phục không còn hiệu lực.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.AttemptsExceeded:
                return Problem(StatusCodes.Status429TooManyRequests, "VERIFY_ATTEMPTS_EXCEEDED",
                    "Đã nhập OTP sai quá số lần cho phép.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.AlreadyVerified:
                return Problem(StatusCodes.Status409Conflict, "ALREADY_VERIFIED",
                    "Phiên khôi phục đã được xác minh.", "Vui lòng tiếp tục đặt mật khẩu mới.");
            case PasswordResetStoreStatus.Busy:
                SetRetryAfter(result.RetryAfter);
                return Problem(StatusCodes.Status409Conflict, "VERIFICATION_IN_PROGRESS",
                    "Phiên khôi phục đang được xác minh.", "Vui lòng thử lại sau.");
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

    private async Task ReleaseQuietlyAsync(Guid recoveryId, string lease)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _store.ReleaseVerificationAsync(recoveryId, lease, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể nhả lease xác minh OTP: {ErrorType}.", exception.GetType().Name);
        }
    }

    private static IActionResult Problem(int status, string code, string title, string detail) =>
        RegisterHandler.CreateProblemResult(status, title, detail, code);

    private static IActionResult StoreUnavailable() => Problem(StatusCodes.Status503ServiceUnavailable,
        "OTP_STORE_UNAVAILABLE", "Không thể xác minh OTP.",
        "Bộ lưu trữ mã chưa sẵn sàng. Vui lòng thử lại sau.");
}
