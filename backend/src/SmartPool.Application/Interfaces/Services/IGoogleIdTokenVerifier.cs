namespace SmartPool.Application.Interfaces.Services;

public interface IGoogleIdTokenVerifier
{
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified);
