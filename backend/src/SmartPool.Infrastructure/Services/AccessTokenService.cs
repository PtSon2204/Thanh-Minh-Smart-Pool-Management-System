using SmartPool.Application.Features.Authentication.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Infrastructure.Services;

public sealed class AccessTokenService : IAccessTokenService
{
    public const string PasswordVersionClaim = "pwd_v";

    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;
    private readonly SigningCredentials _signingCredentials;
    private readonly byte[] _passwordVersionKey;
    private readonly SmartPoolDbContext _context;

    public AccessTokenService(IConfiguration configuration, SmartPoolDbContext context)
    {
        _context = context;
        var secret = configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey must contain at least 32 UTF-8 bytes.");

        _issuer = configuration["JwtSettings:Issuer"]
            ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        _audience = configuration["JwtSettings:Audience"]
            ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
        if (string.IsNullOrWhiteSpace(_issuer) || string.IsNullOrWhiteSpace(_audience))
            throw new InvalidOperationException("JWT issuer and audience must not be empty.");

        if (!int.TryParse(configuration["JwtSettings:AccessTokenExpirationMinutes"], out _expirationMinutes) ||
            _expirationMinutes <= 0)
            throw new InvalidOperationException("JwtSettings:AccessTokenExpirationMinutes must be a positive integer.");

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        _passwordVersionKey = HMACSHA256.HashData(secretBytes,
            Encoding.UTF8.GetBytes("SmartPool/access-token/password-version-key/v1"));
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(secretBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateToken(User user, string role)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty || string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("A user ID and role are required to issue an access token.");
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            throw new ArgumentException("A password hash is required to issue an access token.");

        var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_expirationMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(PasswordVersionClaim, Convert.ToHexString(
                    ComputePasswordVersion(user.Id, user.PasswordHash))),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            IssuedAt = issuedAtUtc,
            NotBefore = issuedAtUtc,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials
        };
        return new AccessTokenResult(new JsonWebTokenHandler().CreateToken(descriptor), expiresAtUtc);
    }

    public async Task<bool> IsCurrentAsync(Guid userId, string tokenRole,
        string tokenPasswordVersion, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(tokenRole) ||
            string.IsNullOrWhiteSpace(tokenPasswordVersion) || tokenPasswordVersion.Length != 64)
            return false;
        byte[] provided;
        try { provided = Convert.FromHexString(tokenPasswordVersion); }
        catch (FormatException) { return false; }

        var passwordHash = await _context.Users.AsNoTracking().Where(user =>
            user.Id == userId && user.IsDeleted != true &&
            user.Status != null && EF.Functions.ILike(user.Status, "ACTIVE") &&
            user.Role != null && user.Role.IsDeleted != true &&
            EF.Functions.ILike(user.Role.Name, tokenRole))
            .Select(user => user.PasswordHash).FirstOrDefaultAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(passwordHash) &&
            CryptographicOperations.FixedTimeEquals(provided, ComputePasswordVersion(userId, passwordHash));
    }

    private byte[] ComputePasswordVersion(Guid userId, string passwordHash) =>
        HMACSHA256.HashData(_passwordVersionKey,
            Encoding.UTF8.GetBytes($"{userId:N}:{passwordHash}"));
}
