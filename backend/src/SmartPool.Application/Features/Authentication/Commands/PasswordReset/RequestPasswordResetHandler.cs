using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class RequestPasswordResetHandler : IRequestHandler<RequestPasswordResetCommand, IActionResult>
{
    internal const string EmailSubject = "[Thanh Minh Smart Pool] Mã khôi phục mật khẩu";

    private readonly IRepository<User> _users;
    private readonly IPasswordResetStore _store;
    private readonly PasswordResetCodeService _codes;
    private readonly IEmailService _email;
    private readonly IHttpContextAccessor _http;
    private readonly IValidator<RequestPasswordResetCommand> _validator;
    private readonly ILogger<RequestPasswordResetHandler> _logger;

    public RequestPasswordResetHandler(
        IRepository<User> users,
        IPasswordResetStore store,
        PasswordResetCodeService codes,
        IEmailService email,
        IHttpContextAccessor http,
        IValidator<RequestPasswordResetCommand> validator,
        ILogger<RequestPasswordResetHandler> logger)
    {
        _users = users;
        _store = store;
        _codes = codes;
        _email = email;
        _http = http;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
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

        var email = request.Email.Trim().ToLowerInvariant();
        var remoteIp = _http.HttpContext?.Connection.RemoteIpAddress;
        if (remoteIp is null)
            return Problem(StatusCodes.Status503ServiceUnavailable, "CLIENT_IP_UNAVAILABLE");
        var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4().ToString() : remoteIp.ToString();

        PasswordResetStoreResult quota;
        try
        {
            // A quota slot is consumed before any account lookup, including for absent emails.
            quota = await _store.TryConsumeRequestQuotaAsync(email, ip, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kiểm tra quota khôi phục mật khẩu: {ErrorType}.", exception.GetType().Name);
            return Problem(StatusCodes.Status503ServiceUnavailable, "OTP_STORE_UNAVAILABLE");
        }
        if (quota.Status == PasswordResetStoreStatus.RateLimited)
        {
            SetRetryAfter(quota.RetryAfter);
            return Problem(StatusCodes.Status429TooManyRequests, "REQUEST_RATE_LIMITED");
        }
        if (quota.Status != PasswordResetStoreStatus.Success)
            return Problem(StatusCodes.Status503ServiceUnavailable, "OTP_STORE_UNAVAILABLE");

        User? user;
        try
        {
            user = await _users.FirstOrDefaultAsync(candidate =>
                candidate.Email != null && candidate.Email.ToLower() == email &&
                candidate.IsDeleted != true && candidate.Status != null &&
                candidate.Status.ToLower() == "active" &&
                candidate.PasswordHash != null && candidate.PasswordHash != "",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể tra cứu tài khoản khôi phục mật khẩu: {ErrorType}.", exception.GetType().Name);
            return Problem(StatusCodes.Status503ServiceUnavailable, "ACCOUNT_LOOKUP_UNAVAILABLE");
        }

        if (user is not null && (user.Id == Guid.Empty || string.IsNullOrWhiteSpace(user.PasswordHash)))
            user = null;

        var recoveryId = Guid.NewGuid();
        var fingerprint = user is null ? null : _codes.ComputePasswordFingerprint(user.Id, user.PasswordHash!);
        var pending = new PendingPasswordReset(recoveryId, user is null ? null : email,
            user?.Id, fingerprint);
        PasswordResetStoreResult started;
        try
        {
            started = await _store.TryStartAsync(pending, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể tạo phiên khôi phục mật khẩu: {ErrorType}.", exception.GetType().Name);
            return Problem(StatusCodes.Status503ServiceUnavailable, "OTP_STORE_UNAVAILABLE");
        }
        if (started.Status != PasswordResetStoreStatus.Success ||
            started.OtpExpiresAtUtc is null || started.RecoveryExpiresAtUtc is null ||
            started.ResendAvailableAtUtc is null)
            return Problem(StatusCodes.Status503ServiceUnavailable, "OTP_STORE_UNAVAILABLE");

        if (user is not null)
        {
            var code = _codes.GenerateCode();
            var digest = _codes.ComputeOtpDigest(recoveryId, code);
            try
            {
                await _email.SendAsync(email, EmailSubject,
                    BuildEmailBody(code, started.OtpExpiresAtUtc.Value), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Keep the inactive session for resend. The public response cannot reveal SMTP outcome.
                _logger.LogWarning("Gửi email khôi phục mật khẩu thất bại: {ErrorType}.", exception.GetType().Name);
                return Accepted(recoveryId, started);
            }

            try
            {
                // Complete activation even if the caller disconnects after SMTP accepted the mail.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                if (!await _store.ActivateInitialCodeAsync(recoveryId, digest, timeout.Token))
                    _logger.LogWarning("Không thể kích hoạt mã khôi phục mật khẩu sau khi gửi email.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Không thể kích hoạt mã khôi phục mật khẩu: {ErrorType}.", exception.GetType().Name);
            }
        }

        return Accepted(recoveryId, started);
    }

    internal static string BuildEmailBody(string code, DateTimeOffset? expiresAtUtc = null)
    {
        var validity = expiresAtUtc is { } expiry
            ? $"Mã có hiệu lực khoảng {Math.Max(1, (int)Math.Ceiling((expiry - DateTimeOffset.UtcNow).TotalMinutes))} phút. "
            : "Mã chỉ có hiệu lực trong thời gian ngắn. ";
        return $"<p>Mã khôi phục mật khẩu của bạn là <strong>{code}</strong>.</p>" +
            $"<p>{validity}Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này.</p>";
    }

    private static IActionResult Accepted(Guid recoveryId, PasswordResetStoreResult stored) =>
        new ObjectResult(new RequestPasswordResetResponse(recoveryId,
            stored.OtpExpiresAtUtc!.Value, stored.RecoveryExpiresAtUtc!.Value,
            stored.ResendAvailableAtUtc!.Value))
        { StatusCode = StatusCodes.Status202Accepted };

    private void SetRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter is { } duration && _http.HttpContext is { } context)
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
    }

    private static IActionResult Problem(int status, string code) => RegisterHandler.CreateProblemResult(
        status,
        status == StatusCodes.Status429TooManyRequests
            ? "Đã gửi quá nhiều yêu cầu khôi phục mật khẩu."
            : "Không thể bắt đầu khôi phục mật khẩu.",
        status == StatusCodes.Status429TooManyRequests
            ? "Vui lòng thử lại sau."
            : "Hệ thống chưa sẵn sàng. Vui lòng thử lại sau.", code);
}
