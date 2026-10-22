using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services;

public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private readonly string? _clientId;

    public GoogleIdTokenVerifier(IConfiguration configuration)
    {
        _clientId = configuration["GoogleAuth:ClientId"];
    }

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_clientId))
            throw new InvalidOperationException("GoogleAuth:ClientId is not configured.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_clientId] });
            return string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email)
                ? null
                : new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
