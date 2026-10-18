using System.Globalization;
using System.Security.Cryptography;
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

public sealed class CompletePasswordResetHandler : IRequestHandler<CompletePasswordResetCommand, IActionResult>
{
    private readonly IPasswordResetStore _store;
    private readonly PasswordResetCodeService _codes;
    private readonly IRepository<User> _users;
    private readonly IUserOperations _userOperations;
    private readonly IPasswordHasher _hasher;
    private readonly IHttpContextAccessor _http;
    private readonly IValidator<CompletePasswordResetCommand> _validator;
    private readonly ILogger<CompletePasswordResetHandler> _logger;

    public CompletePasswordResetHandler(
        IPasswordResetStore store,
        PasswordResetCodeService codes,
        IRepository<User> users,
        IUserOperations userOperations,
        IPasswordHasher hasher,
        IHttpContextAccessor http,
        IValidator<CompletePasswordResetCommand> validator,
        ILogger<CompletePasswordResetHandler> logger)
    {
        _store = store;
        _codes = codes;
        _users = users;
        _userOperations = userOperations;
        _hasher = hasher;
        _http = http;
        _validator = validator;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(CompletePasswordResetCommand request, CancellationToken cancellationToken)
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

        PasswordResetStoreResult grant;
        try
        {
            var digest = _codes.ComputeTokenDigest(request.RecoveryId,
                request.ResetToken.ToUpperInvariant());
            grant = await _store.TryBeginCompletionAsync(request.RecoveryId, digest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể kiểm tra quyền đặt lại mật khẩu: {ErrorType}.", exception.GetType().Name);
            return StoreUnavailable();
        }

        if (grant.Status != PasswordResetStoreStatus.Success)
            return MapGrantResult(grant);
        if (string.IsNullOrWhiteSpace(grant.LeaseId) || grant.Identity is null ||
            grant.Identity.UserId == Guid.Empty || string.IsNullOrWhiteSpace(grant.Identity.Email) ||
            string.IsNullOrWhiteSpace(grant.Identity.PasswordFingerprint))
        {
            if (!string.IsNullOrWhiteSpace(grant.LeaseId))
                await ReleaseQuietlyAsync(request.RecoveryId, grant.LeaseId);
            return StoreUnavailable();
        }

        var lease = grant.LeaseId;
        var identity = grant.Identity;
        var saved = false;
        try
        {
            var user = await _users.FirstOrDefaultAsync(
                candidate => candidate.Id == identity.UserId && candidate.IsDeleted != true &&
                    candidate.Status != null && candidate.Status.ToLower() == "active" &&
                    candidate.Email != null && candidate.Email.ToLower() == identity.Email &&
                    candidate.PasswordHash != null && candidate.PasswordHash != "",
                cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) ||
                !FingerprintMatches(identity.PasswordFingerprint,
                    _codes.ComputePasswordFingerprint(user.Id, user.PasswordHash)))
                return PasswordChanged();

            var newHash = _hasher.HashPassword(user, request.NewPassword);
            // The user operation updates the password and revokes refresh tokens atomically.
            saved = await _userOperations.TryUpdatePasswordAsync(user.Id, identity.Email,
                user.PasswordHash, newHash, cancellationToken);
            if (!saved)
                return PasswordChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("Không thể lưu mật khẩu đã đặt lại: {ErrorType}.", exception.GetType().Name);
            return Problem(StatusCodes.Status503ServiceUnavailable, "PASSWORD_SAVE_FAILED",
                "Không thể đặt lại mật khẩu.", "Hệ thống chưa thể lưu mật khẩu mới. Vui lòng thử lại.");
        }
        finally
        {
            if (!saved) await ReleaseQuietlyAsync(request.RecoveryId, lease);
        }

        // The database change is final. A stale grant cannot update again because the fingerprint changed.
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            if (!await _store.CompleteAsync(request.RecoveryId, lease, timeout.Token))
                _logger.LogWarning("Không thể tiêu thụ reset token sau khi đã lưu mật khẩu.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể tiêu thụ reset token: {ErrorType}.", exception.GetType().Name);
        }

        return new NoContentResult();
    }

    private static bool FingerprintMatches(string stored, string current)
    {
        if (stored.Length != 64) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(stored), Convert.FromHexString(current));
        }
        catch (FormatException) { return false; }
    }

    private IActionResult MapGrantResult(PasswordResetStoreResult result)
    {
        switch (result.Status)
        {
            case PasswordResetStoreStatus.InvalidToken:
                return Problem(StatusCodes.Status400BadRequest, "INVALID_RESET_TOKEN",
                    "Reset token không hợp lệ.", "Vui lòng xác minh OTP lại.");
            case PasswordResetStoreStatus.TokenExpired:
                return Problem(StatusCodes.Status410Gone, "RESET_TOKEN_EXPIRED",
                    "Reset token đã hết hạn.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.Unavailable:
                return Problem(StatusCodes.Status410Gone, "RECOVERY_UNAVAILABLE",
                    "Phiên khôi phục không còn hiệu lực.", "Vui lòng bắt đầu lại quy trình khôi phục.");
            case PasswordResetStoreStatus.Busy:
                SetRetryAfter(result.RetryAfter);
                return Problem(StatusCodes.Status409Conflict, "RECOVERY_IN_PROGRESS",
                    "Phiên khôi phục đang được xử lý.", "Vui lòng thử lại sau.");
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
            await _store.ReleaseCompletionAsync(recoveryId, lease, timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Không thể nhả lease đặt lại mật khẩu: {ErrorType}.", exception.GetType().Name);
        }
    }

    private static IActionResult PasswordChanged() => Problem(StatusCodes.Status409Conflict,
        "PASSWORD_CHANGED", "Không thể đặt lại mật khẩu.",
        "Thông tin tài khoản đã thay đổi. Vui lòng bắt đầu lại quy trình khôi phục.");

    private static IActionResult Problem(int status, string code, string title, string detail) =>
        RegisterHandler.CreateProblemResult(status, title, detail, code);

    private static IActionResult StoreUnavailable() => Problem(StatusCodes.Status503ServiceUnavailable,
        "OTP_STORE_UNAVAILABLE", "Không thể đặt lại mật khẩu.",
        "Bộ lưu trữ quyền đặt lại chưa sẵn sàng. Vui lòng thử lại sau.");
}
