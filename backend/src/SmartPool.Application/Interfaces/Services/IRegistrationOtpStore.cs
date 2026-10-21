using SmartPool.Application.Features.Authentication.Models;
namespace SmartPool.Application.Interfaces.Services;

public interface IRegistrationOtpStore
{
    // Counts every valid registration attempt before database lookups or password hashing.
    Task<RegistrationOtpStoreResult> TryConsumeStartQuotaAsync(
        string email, string ipAddress, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> TryStartAsync(
        PendingRegistration registration, string ipAddress, CancellationToken cancellationToken = default);

    // Only cancels the matching pending session. Rate counters are intentionally retained.
    Task<bool> CancelStartAsync(Guid registrationId, string email, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> TryBeginVerificationAsync(
        Guid registrationId, string otpDigest, CancellationToken cancellationToken = default);

    Task<bool> CompleteVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);

    Task<bool> ReleaseVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);

    // Keep the old OTP active until CompleteResendAsync succeeds.
    Task<RegistrationOtpStoreResult> TryBeginResendAsync(
        Guid registrationId, CancellationToken cancellationToken = default);

    Task<RegistrationOtpStoreResult> CompleteResendAsync(
        Guid registrationId, string leaseId, string newOtpDigest, CancellationToken cancellationToken = default);

    Task<bool> CancelResendAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default);
}
