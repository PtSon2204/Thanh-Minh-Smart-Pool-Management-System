using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartPool.Application.Interfaces.Services;
using StackExchange.Redis;

namespace SmartPool.Infrastructure.Caching;

/// <summary>Atomic password recovery state in a dedicated Redis hash slot.</summary>
public sealed class PasswordResetStore : IPasswordResetStore
{
    private const string Prefix = "SmartPool:password-reset:{password-reset}:";
    private const int LeaseMs = 60_000;

    private const string QuotaScript = """
        if tonumber(redis.call('GET', KEYS[1]) or '0') >= tonumber(ARGV[1]) then
            return {'limited', redis.call('PTTL', KEYS[1])}
        end
        if tonumber(redis.call('GET', KEYS[2]) or '0') >= tonumber(ARGV[2]) then
            return {'limited', redis.call('PTTL', KEYS[2])}
        end
        if redis.call('INCR', KEYS[1]) == 1 then redis.call('PEXPIRE', KEYS[1], 3600000) end
        if redis.call('INCR', KEYS[2]) == 1 then redis.call('PEXPIRE', KEYS[2], 3600000) end
        return {'success'}
        """;

    private const string StartScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('EXISTS', KEYS[1]) == 1 then return {'busy'} end
        local expires = now + tonumber(ARGV[4])
        local otpExpires = math.min(now + tonumber(ARGV[5]), expires)
        local nextResend = now + tonumber(ARGV[6])
        redis.call('HSET', KEYS[1], 'email', ARGV[1], 'userId', ARGV[2],
            'passwordFingerprint', ARGV[3], 'otpDigest', '',
            'otpExpires', otpExpires, 'expires', expires, 'nextResend', nextResend,
            'attempts', 0, 'resends', 0, 'status', 'pending')
        redis.call('PEXPIRE', KEYS[1], ARGV[4])
        return {'success', otpExpires, expires, nextResend}
        """;

    private const string ActivateScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('HGET', KEYS[1], 'status') ~= 'pending' or
            redis.call('HGET', KEYS[1], 'userId') == '' or
            redis.call('HGET', KEYS[1], 'otpDigest') ~= '' or
            tonumber(redis.call('HGET', KEYS[1], 'resends') or '0') ~= 0 or
            tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0') > now or
            tonumber(redis.call('HGET', KEYS[1], 'otpExpires') or '0') <= now or
            tonumber(redis.call('HGET', KEYS[1], 'expires') or '0') <= now then return 0 end
        redis.call('HSET', KEYS[1], 'otpDigest', ARGV[1])
        return 1
        """;

    private const string BeginResendScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        local status = redis.call('HGET', KEYS[1], 'status')
        if not status then return {'unavailable'} end
        local expires = tonumber(redis.call('HGET', KEYS[1], 'expires') or '0')
        if expires <= now then return {'unavailable'} end
        if status == 'verified' or status == 'completed' then return {'alreadyVerified'} end
        if status == 'locked' then return {'attemptsExceeded'} end
        local verifyUntil = tonumber(redis.call('HGET', KEYS[1], 'verifyLeaseUntil') or '0')
        if verifyUntil > now then return {'busy', verifyUntil - now} end
        local resendUntil = tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0')
        if resendUntil > now then return {'busy', resendUntil - now} end
        if tonumber(redis.call('HGET', KEYS[1], 'resends')) >= tonumber(ARGV[2]) then
            return {'resendLimitReached'}
        end
        local nextResend = tonumber(redis.call('HGET', KEYS[1], 'nextResend'))
        if nextResend > now then return {'resendCooldown', nextResend - now} end
        redis.call('HSET', KEYS[1], 'resendLease', ARGV[1], 'resendLeaseUntil', now + tonumber(ARGV[3]))
        return {'success', redis.call('HGET', KEYS[1], 'email'),
            math.min(now + tonumber(ARGV[4]), expires),
            expires, now + tonumber(ARGV[5])}
        """;

    private const string CompleteResendScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('HGET', KEYS[1], 'resendLease') ~= ARGV[1] or
            tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0') <= now then return {'unavailable'} end
        local status = redis.call('HGET', KEYS[1], 'status')
        if status == 'locked' then return {'attemptsExceeded'} end
        if status ~= 'pending' then return {'alreadyVerified'} end
        local verifyUntil = tonumber(redis.call('HGET', KEYS[1], 'verifyLeaseUntil') or '0')
        if verifyUntil > now then return {'busy', verifyUntil - now} end
        local expires = tonumber(redis.call('HGET', KEYS[1], 'expires') or '0')
        if expires <= now then return {'unavailable'} end
        local otpExpires = math.min(now + tonumber(ARGV[3]), expires)
        local nextResend = now + tonumber(ARGV[4])
        local digest = ''
        if redis.call('HGET', KEYS[1], 'userId') ~= '' then digest = ARGV[2] end
        redis.call('HSET', KEYS[1], 'otpDigest', digest, 'otpExpires', otpExpires,
            'nextResend', nextResend)
        redis.call('HINCRBY', KEYS[1], 'resends', 1)
        redis.call('HDEL', KEYS[1], 'resendLease', 'resendLeaseUntil')
        return {'success', otpExpires, expires, nextResend}
        """;

    private const string CancelResendScript = """
        if redis.call('HGET', KEYS[1], 'resendLease') ~= ARGV[1] then return 0 end
        redis.call('HDEL', KEYS[1], 'resendLease', 'resendLeaseUntil')
        return 1
        """;

    private const string FinishResendWithoutNewCodeScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('HGET', KEYS[1], 'resendLease') ~= ARGV[1] or
            tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0') <= now then return {'unavailable'} end
        local status = redis.call('HGET', KEYS[1], 'status')
        if status == 'locked' then return {'attemptsExceeded'} end
        if status ~= 'pending' then return {'alreadyVerified'} end
        local expires = tonumber(redis.call('HGET', KEYS[1], 'expires') or '0')
        if expires <= now then return {'unavailable'} end
        local nextResend = now + tonumber(ARGV[3])
        local advertisedOtpExpires = math.min(now + tonumber(ARGV[2]), expires)
        redis.call('HSET', KEYS[1], 'nextResend', nextResend)
        if redis.call('HGET', KEYS[1], 'userId') == '' then
            redis.call('HSET', KEYS[1], 'otpExpires', advertisedOtpExpires)
        end
        redis.call('HINCRBY', KEYS[1], 'resends', 1)
        redis.call('HDEL', KEYS[1], 'resendLease', 'resendLeaseUntil')
        return {'success', advertisedOtpExpires, expires, nextResend}
        """;

    private const string BeginVerifyScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        local status = redis.call('HGET', KEYS[1], 'status')
        if not status then return {'unavailable'} end
        if status == 'verified' or status == 'completed' then return {'alreadyVerified'} end
        if status == 'locked' then return {'attemptsExceeded'} end
        local verifyUntil = tonumber(redis.call('HGET', KEYS[1], 'verifyLeaseUntil') or '0')
        if verifyUntil > now then return {'busy', verifyUntil - now} end
        if tonumber(redis.call('HGET', KEYS[1], 'otpExpires')) <= now then return {'otpExpired'} end
        local digest = redis.call('HGET', KEYS[1], 'otpDigest')
        if digest == '' or redis.call('HGET', KEYS[1], 'userId') == '' or digest ~= ARGV[1] then
            local attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
            if attempts >= tonumber(ARGV[3]) then
                redis.call('HSET', KEYS[1], 'status', 'locked')
                return {'attemptsExceeded'}
            end
            return {'invalidOtp'}
        end
        redis.call('HSET', KEYS[1], 'verifyLease', ARGV[2], 'verifyLeaseUntil', now + tonumber(ARGV[4]))
        return {'success', redis.call('HGET', KEYS[1], 'userId'),
            redis.call('HGET', KEYS[1], 'email'), redis.call('HGET', KEYS[1], 'passwordFingerprint')}
        """;

    private const string CompleteVerifyScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('HGET', KEYS[1], 'status') ~= 'pending' or
            redis.call('HGET', KEYS[1], 'verifyLease') ~= ARGV[1] or
            tonumber(redis.call('HGET', KEYS[1], 'verifyLeaseUntil') or '0') <= now then return {'unavailable'} end
        local expires = now + tonumber(ARGV[3])
        redis.call('HSET', KEYS[2], 'tokenDigest', ARGV[2],
            'userId', redis.call('HGET', KEYS[1], 'userId'),
            'email', redis.call('HGET', KEYS[1], 'email'),
            'passwordFingerprint', redis.call('HGET', KEYS[1], 'passwordFingerprint'))
        redis.call('PEXPIRE', KEYS[2], ARGV[3])
        redis.call('HSET', KEYS[1], 'status', 'verified')
        redis.call('HDEL', KEYS[1], 'otpDigest', 'verifyLease', 'verifyLeaseUntil',
            'resendLease', 'resendLeaseUntil', 'email', 'userId', 'passwordFingerprint')
        return {'success', expires}
        """;

    private const string ReleaseVerifyScript = """
        if redis.call('HGET', KEYS[1], 'status') ~= 'pending' or
            redis.call('HGET', KEYS[1], 'verifyLease') ~= ARGV[1] then return 0 end
        redis.call('HDEL', KEYS[1], 'verifyLease', 'verifyLeaseUntil')
        return 1
        """;

    private const string BeginCompleteScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        if redis.call('EXISTS', KEYS[1]) == 0 then
            if redis.call('HGET', KEYS[2], 'status') == 'verified' then return {'tokenExpired'} end
            return {'unavailable'}
        end
        if redis.call('HGET', KEYS[1], 'tokenDigest') ~= ARGV[1] then return {'invalidToken'} end
        local untilTime = tonumber(redis.call('HGET', KEYS[1], 'completionLeaseUntil') or '0')
        if untilTime > now then return {'busy', untilTime - now} end
        redis.call('HSET', KEYS[1], 'completionLease', ARGV[2],
            'completionLeaseUntil', now + tonumber(ARGV[3]))
        return {'success', redis.call('HGET', KEYS[1], 'userId'),
            redis.call('HGET', KEYS[1], 'email'), redis.call('HGET', KEYS[1], 'passwordFingerprint')}
        """;

    private const string CompleteScript = """
        if redis.call('HGET', KEYS[1], 'completionLease') ~= ARGV[1] then return 0 end
        redis.call('DEL', KEYS[1])
        if redis.call('HGET', KEYS[2], 'status') == 'verified' then
            redis.call('HSET', KEYS[2], 'status', 'completed')
        end
        return 1
        """;

    private const string ReleaseCompleteScript = """
        if redis.call('HGET', KEYS[1], 'completionLease') ~= ARGV[1] then return 0 end
        redis.call('HDEL', KEYS[1], 'completionLease', 'completionLeaseUntil')
        return 1
        """;

    private readonly IDatabase _db;
    private readonly byte[] _key;
    private readonly int _requestMs, _codeMs, _grantMs, _cooldownMs;
    private readonly int _maxAttempts, _maxResends, _maxEmailRequests, _maxIpRequests;

    public PasswordResetStore(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _db = redis.GetDatabase();
        var settings = configuration.GetSection("PasswordReset");
        _key = ReadKey(settings["HmacKey"]);
        _requestMs = ReadPositive(settings, "RequestTtlMinutes", 30) * 60_000;
        _codeMs = ReadPositive(settings, "CodeTtlMinutes", 5) * 60_000;
        _grantMs = ReadPositive(settings, "GrantTtlMinutes", 10) * 60_000;
        _cooldownMs = ReadPositive(settings, "ResendCooldownSeconds", 60) * 1_000;
        _maxAttempts = ReadPositive(settings, "MaxVerifyAttempts", 5);
        _maxResends = ReadPositive(settings, "MaxResends", 3);
        _maxEmailRequests = ReadPositive(settings, "MaxRequestsPerEmailPerHour", 3);
        _maxIpRequests = ReadPositive(settings, "MaxRequestsPerIpPerHour", 20);
        if (_codeMs > _requestMs)
            throw new InvalidOperationException("PasswordReset:CodeTtlMinutes cannot exceed RequestTtlMinutes.");
        var registrationKey = configuration["RegistrationOtp:HmacKey"];
        if (!string.IsNullOrWhiteSpace(registrationKey) &&
            CryptographicOperations.FixedTimeEquals(_key, ReadKey(registrationKey)))
            throw new InvalidOperationException("PasswordReset:HmacKey must differ from RegistrationOtp:HmacKey.");
    }

    public async Task<PasswordResetStoreResult> TryConsumeRequestQuotaAsync(
        string normalizedEmail, string ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        return Parse(await Run(QuotaScript,
            [RateKey("email", normalizedEmail), RateKey("ip", ipAddress)],
            [_maxEmailRequests, _maxIpRequests], cancellationToken));
    }

    public async Task<PasswordResetStoreResult> TryStartAsync(
        PendingPasswordReset request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecoveryId == Guid.Empty ||
            (request.UserId.HasValue && (request.UserId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PasswordFingerprint))) ||
            (!request.UserId.HasValue && (request.Email is not null || request.PasswordFingerprint is not null)))
            throw new ArgumentException("The recovery session must have a complete identity or be a decoy.");
        return Parse(await Run(StartScript, [SessionKey(request.RecoveryId)],
            [request.Email ?? "", request.UserId?.ToString("N") ?? "",
                request.PasswordFingerprint ?? "", _requestMs, _codeMs, _cooldownMs], cancellationToken));
    }

    public async Task<bool> ActivateInitialCodeAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default) =>
        (long)await Run(ActivateScript, [SessionKey(recoveryId)],
            [RequireDigest(otpDigest)], cancellationToken) == 1;

    public async Task<PasswordResetStoreResult> TryBeginResendAsync(
        Guid recoveryId, CancellationToken cancellationToken = default)
    {
        var lease = Guid.NewGuid().ToString("N");
        var values = Items(await Run(BeginResendScript, [SessionKey(recoveryId)],
            [lease, _maxResends, LeaseMs, _codeMs, _cooldownMs], cancellationToken));
        var result = Text(values[0]) == "success"
            ? new PasswordResetStoreResult(PasswordResetStoreStatus.Success,
                Instant(values[2]), Instant(values[3]), Instant(values[4]))
            : Parse(values);
        return result.Status == PasswordResetStoreStatus.Success
            ? result with { LeaseId = lease, Email = Text(values[1]) is { Length: > 0 } email ? email : null }
            : result;
    }

    public async Task<PasswordResetStoreResult> CompleteResendAsync(
        Guid recoveryId, string leaseId, string otpDigest, CancellationToken cancellationToken = default) =>
        Parse(await Run(CompleteResendScript, [SessionKey(recoveryId)],
            [leaseId, RequireDigest(otpDigest), _codeMs, _cooldownMs], cancellationToken));

    public async Task<PasswordResetStoreResult> FinishResendWithoutNewCodeAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default) =>
        Parse(await Run(FinishResendWithoutNewCodeScript, [SessionKey(recoveryId)],
            [leaseId, _codeMs, _cooldownMs], cancellationToken));

    public async Task<bool> CancelResendAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default) =>
        (long)await Run(CancelResendScript, [SessionKey(recoveryId)], [leaseId], cancellationToken) == 1;

    public async Task<PasswordResetStoreResult> TryBeginVerificationAsync(
        Guid recoveryId, string otpDigest, CancellationToken cancellationToken = default)
    {
        var lease = Guid.NewGuid().ToString("N");
        var values = Items(await Run(BeginVerifyScript, [SessionKey(recoveryId)],
            [RequireDigest(otpDigest), lease, _maxAttempts, LeaseMs], cancellationToken));
        var result = Text(values[0]) == "success"
            ? new PasswordResetStoreResult(PasswordResetStoreStatus.Success)
            : Parse(values);
        return result.Status == PasswordResetStoreStatus.Success
            ? result with { LeaseId = lease, Identity = Identity(values) }
            : result;
    }

    public async Task<PasswordResetStoreResult> CompleteVerificationAsync(
        Guid recoveryId, string leaseId, string resetTokenDigest, CancellationToken cancellationToken = default) =>
        Parse(await Run(CompleteVerifyScript, [SessionKey(recoveryId), GrantKey(recoveryId)],
            [leaseId, RequireDigest(resetTokenDigest), _grantMs], cancellationToken));

    public async Task<bool> ReleaseVerificationAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default) =>
        (long)await Run(ReleaseVerifyScript, [SessionKey(recoveryId)], [leaseId], cancellationToken) == 1;

    public async Task<PasswordResetStoreResult> TryBeginCompletionAsync(
        Guid recoveryId, string resetTokenDigest, CancellationToken cancellationToken = default)
    {
        var lease = Guid.NewGuid().ToString("N");
        var values = Items(await Run(BeginCompleteScript, [GrantKey(recoveryId), SessionKey(recoveryId)],
            [RequireDigest(resetTokenDigest), lease, LeaseMs], cancellationToken));
        var result = Text(values[0]) == "success"
            ? new PasswordResetStoreResult(PasswordResetStoreStatus.Success)
            : Parse(values);
        return result.Status == PasswordResetStoreStatus.Success
            ? result with { LeaseId = lease, Identity = Identity(values) }
            : result;
    }

    public async Task<bool> CompleteAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default) =>
        (long)await Run(CompleteScript, [GrantKey(recoveryId), SessionKey(recoveryId)],
            [leaseId], cancellationToken) == 1;

    public async Task<bool> ReleaseCompletionAsync(
        Guid recoveryId, string leaseId, CancellationToken cancellationToken = default) =>
        (long)await Run(ReleaseCompleteScript, [GrantKey(recoveryId)], [leaseId], cancellationToken) == 1;

    private async Task<RedisResult> Run(
        string script, RedisKey[] keys, RedisValue[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.ScriptEvaluateAsync(script, keys, args).WaitAsync(cancellationToken);
    }

    private static PasswordResetStoreResult Parse(RedisResult raw) => Parse(Items(raw));
    private static RedisResult[] Items(RedisResult raw) => (RedisResult[]?)raw ??
        throw new InvalidOperationException("Redis returned an unexpected password reset result.");
    private static PasswordResetStoreResult Parse(RedisResult[] values) => Text(values[0]) switch
    {
        "success" => values.Length == 4
            ? new(PasswordResetStoreStatus.Success, Instant(values[1]), Instant(values[2]), Instant(values[3]))
            : values.Length == 2
                ? new(PasswordResetStoreStatus.Success, ResetTokenExpiresAtUtc: Instant(values[1]))
                : new(PasswordResetStoreStatus.Success),
        "limited" => new(PasswordResetStoreStatus.RateLimited, RetryAfter: Duration(values[1])),
        "unavailable" => new(PasswordResetStoreStatus.Unavailable),
        "otpExpired" => new(PasswordResetStoreStatus.OtpExpired),
        "invalidOtp" => new(PasswordResetStoreStatus.InvalidOtp),
        "attemptsExceeded" => new(PasswordResetStoreStatus.AttemptsExceeded),
        "resendCooldown" => new(PasswordResetStoreStatus.ResendCooldown, RetryAfter: Duration(values[1])),
        "resendLimitReached" => new(PasswordResetStoreStatus.ResendLimitReached),
        "busy" => new(PasswordResetStoreStatus.Busy,
            RetryAfter: values.Length > 1 ? Duration(values[1]) : null),
        "alreadyVerified" => new(PasswordResetStoreStatus.AlreadyVerified),
        "invalidToken" => new(PasswordResetStoreStatus.InvalidToken),
        "tokenExpired" => new(PasswordResetStoreStatus.TokenExpired),
        var status => throw new InvalidOperationException($"Unexpected password reset store status: {status}")
    };

    private static PasswordResetIdentity Identity(RedisResult[] values) =>
        new(Guid.ParseExact(Text(values[1]), "N"), Text(values[2]), Text(values[3]));
    private static string Text(RedisResult value) => value.ToString() ?? string.Empty;
    private static DateTimeOffset Instant(RedisResult value) => DateTimeOffset.FromUnixTimeMilliseconds((long)value);
    private static TimeSpan Duration(RedisResult value) => TimeSpan.FromMilliseconds(Math.Max(0, (long)value));
    private static string Id(Guid id) => id.ToString("N");
    private static string RequireDigest(string digest) => !string.IsNullOrWhiteSpace(digest)
        ? digest : throw new ArgumentException("A digest is required.", nameof(digest));
    private static RedisKey SessionKey(Guid id) => Prefix + "session:" + Id(id);
    private static RedisKey GrantKey(Guid id) => Prefix + "grant:" + Id(id);
    private RedisKey RateKey(string kind, string value) => Prefix + "rate:" + kind + ":" +
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(kind + ":" + value)));

    private static byte[] ReadKey(string? encoded)
    {
        byte[] key;
        try { key = Convert.FromBase64String(encoded ?? string.Empty); }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("PasswordReset:HmacKey must be base64.", exception);
        }
        if (key.Length < 32)
            throw new InvalidOperationException("PasswordReset:HmacKey must contain at least 32 random bytes.");
        return key;
    }

    private static int ReadPositive(IConfiguration section, string name, int fallback)
    {
        var value = section.GetValue(name, fallback);
        if (value <= 0 || value > 35_000)
            throw new InvalidOperationException($"PasswordReset:{name} must be positive and within supported range.");
        return value;
    }
}
