namespace SmartPool.Application.Features.Authentication.Models;

public enum PasswordResetStoreStatus
{
    Success, RateLimited, Unavailable, OtpExpired, InvalidOtp, AttemptsExceeded,
    ResendCooldown, ResendLimitReached, Busy, AlreadyVerified, InvalidToken, TokenExpired
}
