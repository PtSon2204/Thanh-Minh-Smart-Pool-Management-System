using SmartPool.Application.Features.Authentication.Models;
namespace SmartPool.Application.Interfaces.Services;

public interface IPasswordResetStore
{
    Task<PasswordResetStoreResult> TryConsumeRequestQuotaAsync(
        string normalizedEmail, string ipAddress, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryStartAsync(
        PendingPasswordReset request, CancellationToken cancellationToken = default);
    // Activate only after the initial email was sent. A decoy never receives an active code.
    Task<bool> ActivateInitialCodeAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginResendAsync(
        Guid recoveryId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> CompleteResendAsync(
        Guid recoveryId, string leaseId, string otpDigest, CancellationToken cancellationToken = default);
    // Consume the resend attempt and cooldown without replacing the previous OTP.
    // Also used for decoy sessions so public timing remains indistinguishable.
    Task<PasswordResetStoreResult> FinishResendWithoutNewCodeAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<bool> CancelResendAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginVerificationAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> CompleteVerificationAsync(
        Guid recoveryId, string leaseId, string resetTokenDigest, CancellationToken cancellationToken = default);
    Task<bool> ReleaseVerificationAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<PasswordResetStoreResult> TryBeginCompletionAsync(
        Guid recoveryId, string resetTokenDigest, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseCompletionAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default);
}
