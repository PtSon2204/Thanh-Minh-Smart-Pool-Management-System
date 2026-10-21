namespace SmartPool.Application.Features.Authentication.Models;

public enum RegistrationOtpStoreStatus
{
    Success,
    PendingRegistration,
    RateLimited,
    Unavailable,
    OtpExpired,
    InvalidOtp,
    AttemptsExceeded,
    ResendCooldown,
    ResendLimitReached,
    Busy,
    AlreadyVerified
}
