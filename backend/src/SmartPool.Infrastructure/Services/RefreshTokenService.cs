using SmartPool.Application.Features.Authentication.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenByteCount = 32;
    private const int SecretLength = 43;

    private readonly IAccessTokenService _accessTokens;
    private readonly SmartPoolDbContext _context;
    private readonly int _expirationDays;

    public RefreshTokenService(
        IConfiguration configuration,
        IAccessTokenService accessTokens,
        SmartPoolDbContext context)
    {
        _accessTokens = accessTokens;
        _context = context;

        if (!int.TryParse(configuration["JwtSettings:RefreshTokenExpirationDays"], out _expirationDays) ||
            _expirationDays <= 0 || _expirationDays > 36500)
            throw new InvalidOperationException(
                "JwtSettings:RefreshTokenExpirationDays must be a positive integer no greater than 36500.");
    }

    public async Task<RefreshTokenResult?> CreateAsync(
        Guid userId, string? expectedPasswordHash, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A user ID is required to issue a refresh token.", nameof(userId));
        if (expectedPasswordHash is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedPasswordHash);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted == true ||
            !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(user.PasswordHash, expectedPasswordHash, StringComparison.Ordinal))
            return null;

        var token = GenerateToken(Guid.NewGuid());
        var createdAtUtc = DateTime.UtcNow;
        var expiresAtUtc = createdAtUtc.AddDays(_expirationDays);

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = ComputeHash(token),
            CreatedAt = createdAtUtc,
            ExpiredAt = expiresAtUtc
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RefreshTokenResult(token, expiresAtUtc);
    }

    public string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        if (!TryGetFamily(token, out var family))
            return digest;

        var familyHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(family.ToString("N"))));
        return $"{familyHash}:{digest}";
    }

    public async Task<RefreshRotationResult?> RotateAsync(
        string token, CancellationToken cancellationToken = default)
    {
        if (!IsTokenShapeValid(token))
            return null;

        var tokenHash = ComputeHash(token);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var current = await _context.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        var now = DateTime.UtcNow;
        if (current is null)
            return null;
        if (current.RevokeAt is not null)
        {
            await RevokeFamilyAsync(current, now, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (current.ExpiredAt <= now)
            return null;

        // Serialize refresh with password changes: both paths lock the user row
        // before changing refresh-token rows.
        var user = await LockUserAsync(current.UserId, cancellationToken);
        if (user is null || user.IsDeleted == true ||
            !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            user.RoleId is null)
            return null;

        var role = await _context.Roles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == user.RoleId.Value, cancellationToken);
        if (role is null || role.IsDeleted == true || string.IsNullOrWhiteSpace(role.Name))
            return null;

        var coachAvailable = user.IsCoach && string.Equals(role.Name, "CUSTOMER",
                StringComparison.OrdinalIgnoreCase) &&
            await _context.Roles.AsNoTracking().AnyAsync(
            item => item.Name == "COACH" && item.IsDeleted != true, cancellationToken);
        var effectiveRoles = coachAvailable && !string.Equals(role.Name, "COACH",
            StringComparison.OrdinalIgnoreCase) ? new[] { role.Name, "COACH" } : new[] { role.Name };
        var accessToken = _accessTokens.CreateToken(user, role.Name, effectiveRoles);
        var family = TryGetFamily(token, out var existingFamily) ? existingFamily : Guid.NewGuid();
        var newToken = GenerateToken(family);
        var expiresAtUtc = now.AddDays(_expirationDays);

        // The conditional update is the single-use gate. Concurrent requests can read the same
        // token, but PostgreSQL allows only one of them to update its unrevoked row.
        var updated = await _context.RefreshTokens
            .Where(item => item.Id == current.Id && item.TokenHash == tokenHash &&
                item.RevokeAt == null && item.ExpiredAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokeAt, now),
                cancellationToken);
        if (updated != 1)
        {
            await RevokeFamilyAsync(current, now, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = ComputeHash(newToken),
            CreatedAt = now,
            ExpiredAt = expiresAtUtc
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RefreshRotationResult(user.Id, user.Username ?? string.Empty, role.Name,
            accessToken.Token, accessToken.ExpiresAtUtc,
            newToken, expiresAtUtc) { Roles = effectiveRoles };
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!IsTokenShapeValid(token))
            return;

        var tokenHash = ComputeHash(token);
        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var current = await _context.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (current is null)
            return;

        await RevokeFamilyAsync(current, now, current.RevokeAt is not null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(
        RefreshToken token, DateTime now, bool legacyAllSessions,
        CancellationToken cancellationToken)
    {
        var separator = token.TokenHash.IndexOf(':');
        if (separator == 64)
        {
            var prefix = token.TokenHash[..(separator + 1)];
            await _context.RefreshTokens
                .Where(item => item.TokenHash.StartsWith(prefix) && item.RevokeAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokeAt, now),
                    cancellationToken);
        }
        else
        {
            // Legacy tokens have no family identifier. Reuse invalidates every active
            // token of that account; ordinary logout only invalidates this token.
            var candidates = _context.RefreshTokens.Where(item => item.RevokeAt == null);
            candidates = legacyAllSessions
                ? candidates.Where(item => item.UserId == token.UserId)
                : candidates.Where(item => item.Id == token.Id);
            await candidates.ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokeAt, now), cancellationToken);
        }
    }

    private static string GenerateToken(Guid family) =>
        $"{family:N}." + Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteCount))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool IsTokenShapeValid(string? token) => token is not null &&
        ((token.Length == SecretLength && IsBase64Url(token)) ||
         (TryGetFamily(token, out _) && IsBase64Url(token[(32 + 1)..])));

    private static bool TryGetFamily(string token, out Guid family)
    {
        family = default;
        return token.Length == 32 + 1 + SecretLength && token[32] == '.' &&
            Guid.TryParseExact(token[..32], "N", out family);
    }

    private static bool IsBase64Url(string value) =>
        value.Length == SecretLength && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.Users
            .FromSqlInterpolated($"SELECT * FROM smart_pool.users WHERE id = {userId} FOR UPDATE")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
}
