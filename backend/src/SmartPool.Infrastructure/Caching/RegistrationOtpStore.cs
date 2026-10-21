using SmartPool.Application.Features.Authentication.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartPool.Application.Interfaces.Services;
using StackExchange.Redis;

namespace SmartPool.Infrastructure.Caching;

/// <summary>
/// Redis state machine for pending registrations. All keys use one hash slot so
/// multi-key scripts also work on Redis Cluster. Redis TIME is the clock source.
/// </summary>
public sealed class RegistrationOtpStore : IRegistrationOtpStore
{
    private const string Prefix = "SmartPool:registration-otp:{registration-otp}:";
    private const int LeaseMilliseconds = 60_000;

    private const string StartScript = """
        local nowParts = redis.call('TIME')
        local now = nowParts[1] * 1000 + math.floor(nowParts[2] / 1000)
        if redis.call('EXISTS', KEYS[1]) == 1 or redis.call('EXISTS', KEYS[2]) == 1 then
            return {'pending'}
        end
        local expires = now + tonumber(ARGV[10])
        local otpExpires = math.min(now + tonumber(ARGV[11]), expires)
        local nextResend = now + tonumber(ARGV[12])
        redis.call('SET', KEYS[2], ARGV[1], 'PX', ARGV[10])
        redis.call('HSET', KEYS[1],
            'username', ARGV[2], 'email', ARGV[3], 'phone', ARGV[4],
            'passwordHash', ARGV[5], 'fullName', ARGV[6],
            'dateOfBirth', ARGV[7], 'address', ARGV[8], 'otpDigest', ARGV[9],
            'otpExpires', otpExpires, 'expires', expires,
            'nextResend', nextResend, 'attempts', 0, 'resends', 0,
            'status', 'pending', 'verified', 0)
        redis.call('PEXPIRE', KEYS[1], ARGV[10])
        return {'success', otpExpires, expires, nextResend}
        """;

    private const string ConsumeStartQuotaScript = """
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

    private const string CancelStartScript = """
        if redis.call('GET', KEYS[2]) ~= ARGV[1] then return 0 end
        if redis.call('HGET', KEYS[1], 'status') ~= 'pending' then return 0 end
        redis.call('DEL', KEYS[1], KEYS[2])
        return 1
        """;

    private const string BeginVerifyScript = """
        local nowParts = redis.call('TIME')
        local now = nowParts[1] * 1000 + math.floor(nowParts[2] / 1000)
        local status = redis.call('HGET', KEYS[1], 'status')
        if not status then return {'unavailable'} end
        if status == 'completed' then return {'alreadyVerified'} end
        if status == 'locked' then return {'attemptsExceeded'} end
        local leaseUntil = tonumber(redis.call('HGET', KEYS[1], 'verifyLeaseUntil') or '0')
        if status == 'processing' and leaseUntil > now then
            return {'busy', leaseUntil - now}
        end
        if tonumber(redis.call('HGET', KEYS[1], 'otpExpires')) <= now then
            return {'otpExpired'}
        end
        if redis.call('HGET', KEYS[1], 'otpDigest') ~= ARGV[1] then
            local attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
            if attempts >= tonumber(ARGV[3]) then
                redis.call('HSET', KEYS[1], 'status', 'locked')
                return {'attemptsExceeded'}
            end
            return {'invalidOtp'}
        end
        redis.call('HSET', KEYS[1], 'verified', 1, 'status', 'processing',
            'verifyLease', ARGV[2], 'verifyLeaseUntil', now + tonumber(ARGV[4]))
        return {'success',
            redis.call('HGET', KEYS[1], 'username'),
            redis.call('HGET', KEYS[1], 'email'),
            redis.call('HGET', KEYS[1], 'phone'),
            redis.call('HGET', KEYS[1], 'passwordHash'),
            redis.call('HGET', KEYS[1], 'fullName'),
            redis.call('HGET', KEYS[1], 'dateOfBirth'),
            redis.call('HGET', KEYS[1], 'address')}
        """;

    private const string CompleteVerifyScript = """
        if redis.call('HGET', KEYS[1], 'status') ~= 'processing' or
            redis.call('HGET', KEYS[1], 'verifyLease') ~= ARGV[1] then return 0 end
        redis.call('HSET', KEYS[1], 'status', 'completed')
        redis.call('HDEL', KEYS[1], 'username', 'email', 'phone', 'passwordHash',
            'fullName', 'dateOfBirth', 'address', 'otpDigest', 'verifyLease',
            'verifyLeaseUntil', 'resendLease', 'resendLeaseUntil')
        if redis.call('GET', KEYS[2]) == ARGV[2] then redis.call('DEL', KEYS[2]) end
        return 1
        """;

    private const string ReleaseVerifyScript = """
        if redis.call('HGET', KEYS[1], 'status') ~= 'processing' or
            redis.call('HGET', KEYS[1], 'verifyLease') ~= ARGV[1] then return 0 end
        redis.call('HSET', KEYS[1], 'status', 'pending', 'verified', 0)
        redis.call('HDEL', KEYS[1], 'verifyLease', 'verifyLeaseUntil')
        return 1
        """;

    private const string BeginResendScript = """
        local nowParts = redis.call('TIME')
        local now = nowParts[1] * 1000 + math.floor(nowParts[2] / 1000)
        local status = redis.call('HGET', KEYS[1], 'status')
        if not status then return {'unavailable'} end
        if status == 'completed' or redis.call('HGET', KEYS[1], 'verified') == '1' then
            return {'alreadyVerified'}
        end
        if status == 'locked' then return {'attemptsExceeded'} end
        local leaseUntil = tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0')
        if leaseUntil > now then return {'busy', leaseUntil - now} end
        if tonumber(redis.call('HGET', KEYS[1], 'resends')) >= tonumber(ARGV[2]) then
            return {'resendLimitReached'}
        end
        local nextResend = tonumber(redis.call('HGET', KEYS[1], 'nextResend'))
        if nextResend > now then return {'resendCooldown', nextResend - now} end
        redis.call('HSET', KEYS[1], 'resendLease', ARGV[1],
            'resendLeaseUntil', now + tonumber(ARGV[3]))
        return {'success', redis.call('HGET', KEYS[1], 'email')}
        """;

    private const string CompleteResendScript = """
        local nowParts = redis.call('TIME')
        local now = nowParts[1] * 1000 + math.floor(nowParts[2] / 1000)
        if redis.call('HGET', KEYS[1], 'resendLease') ~= ARGV[1] or
            tonumber(redis.call('HGET', KEYS[1], 'resendLeaseUntil') or '0') <= now then
            return {'unavailable'}
        end
        if redis.call('HGET', KEYS[1], 'status') ~= 'pending' or
            redis.call('HGET', KEYS[1], 'verified') == '1' then return {'alreadyVerified'} end
        local expires = tonumber(redis.call('HGET', KEYS[1], 'expires'))
        if not expires or expires <= now then return {'unavailable'} end
        local otpExpires = math.min(now + tonumber(ARGV[3]), expires)
        local nextResend = now + tonumber(ARGV[4])
        redis.call('HSET', KEYS[1], 'otpDigest', ARGV[2], 'otpExpires', otpExpires,
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

    private readonly IDatabase _database;
    private readonly byte[] _hmacKey;
    private readonly int _pendingMs;
    private readonly int _otpMs;
    private readonly int _cooldownMs;
    private readonly int _maxAttempts;
    private readonly int _maxResends;
    private readonly int _maxStartsPerEmail;
    private readonly int _maxStartsPerIp;

    public RegistrationOtpStore(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _database = redis.GetDatabase();
        var section = configuration.GetSection("RegistrationOtp");
        _pendingMs = ReadPositive(section, "PendingTtlMinutes", 30) * 60_000;
        _otpMs = ReadPositive(section, "CodeTtlMinutes", 5) * 60_000;
        _cooldownMs = ReadPositive(section, "ResendCooldownSeconds", 60) * 1_000;
        _maxAttempts = ReadPositive(section, "MaxVerifyAttempts", 5);
        _maxResends = ReadPositive(section, "MaxResends", 3);
        _maxStartsPerEmail = ReadPositive(section, "MaxStartsPerEmailPerHour", 3);
        _maxStartsPerIp = ReadPositive(section, "MaxStartsPerIpPerHour", 20);
        if (_otpMs > _pendingMs)
            throw new InvalidOperationException("RegistrationOtp:CodeTtlMinutes cannot exceed PendingTtlMinutes.");

        try { _hmacKey = Convert.FromBase64String(section["HmacKey"] ?? string.Empty); }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("RegistrationOtp:HmacKey must be base64.", exception);
        }
        if (_hmacKey.Length < 32)
            throw new InvalidOperationException("RegistrationOtp:HmacKey must contain at least 32 random bytes.");
    }

    public async Task<RegistrationOtpStoreResult> TryStartAsync(
        PendingRegistration registration, string ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (registration.RegistrationId == Guid.Empty || string.IsNullOrWhiteSpace(ipAddress))
            throw new ArgumentException("RegistrationId and IP address are required.");
        var keys = new RedisKey[]
        {
            SessionKey(registration.RegistrationId), EmailKey(registration.Email)
        };
        var args = new RedisValue[]
        {
            Id(registration.RegistrationId), registration.Username, registration.Email,
            registration.Phone, registration.PasswordHash, registration.FullName,
            registration.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            registration.Address ?? string.Empty, registration.OtpDigest,
            _pendingMs, _otpMs, _cooldownMs
        };
        return Parse(await RunAsync(StartScript, keys, args, cancellationToken));
    }

    public async Task<RegistrationOtpStoreResult> TryConsumeStartQuotaAsync(
        string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        return Parse(await RunAsync(ConsumeStartQuotaScript,
            [RateKey("email", email), RateKey("ip", ipAddress)],
            [_maxStartsPerEmail, _maxStartsPerIp], cancellationToken));
    }

    public async Task<bool> CancelStartAsync(Guid registrationId, string email, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(CancelStartScript,
            [SessionKey(registrationId), EmailKey(email)], [Id(registrationId)], cancellationToken);
        return (long)result == 1;
    }

    public async Task<RegistrationOtpStoreResult> TryBeginVerificationAsync(
        Guid registrationId, string otpDigest, CancellationToken cancellationToken = default)
    {
        var leaseId = Guid.NewGuid().ToString("N");
        var values = ArrayResult(await RunAsync(BeginVerifyScript, [SessionKey(registrationId)],
            [otpDigest, leaseId, _maxAttempts, LeaseMilliseconds], cancellationToken));
        var result = Parse(values);
        if (result.Status != RegistrationOtpStoreStatus.Success) return result;
        var dateText = Value(values[6]);
        return result with
        {
            LeaseId = leaseId,
            Registration = new VerifiedRegistration(
                Value(values[1]), Value(values[2]), Value(values[3]), Value(values[4]),
                Value(values[5]), dateText.Length == 0 ? null : DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Value(values[7]) is { Length: > 0 } address ? address : null)
        };
    }

    public async Task<bool> CompleteVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default)
    {
        // Email key is derived from the still-pending hash, without exposing it to the caller.
        var email = await _database.HashGetAsync(SessionKey(registrationId), "email").WaitAsync(cancellationToken);
        if (email.IsNullOrEmpty) return false;
        var result = await RunAsync(CompleteVerifyScript,
            [SessionKey(registrationId), EmailKey(email!)], [leaseId, Id(registrationId)], cancellationToken);
        return (long)result == 1;
    }

    public async Task<bool> ReleaseVerificationAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(ReleaseVerifyScript, [SessionKey(registrationId)], [leaseId], cancellationToken);
        return (long)result == 1;
    }

    public async Task<RegistrationOtpStoreResult> TryBeginResendAsync(
        Guid registrationId, CancellationToken cancellationToken = default)
    {
        var leaseId = Guid.NewGuid().ToString("N");
        var result = ArrayResult(await RunAsync(BeginResendScript, [SessionKey(registrationId)],
            [leaseId, _maxResends, LeaseMilliseconds], cancellationToken));
        var parsed = Parse(result);
        return parsed.Status == RegistrationOtpStoreStatus.Success
            ? parsed with { LeaseId = leaseId, Email = Value(result[1]) }
            : parsed;
    }

    public async Task<RegistrationOtpStoreResult> CompleteResendAsync(
        Guid registrationId, string leaseId, string newOtpDigest, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(CompleteResendScript, [SessionKey(registrationId)],
            [leaseId, newOtpDigest, _otpMs, _cooldownMs], cancellationToken);
        return Parse(result);
    }

    public async Task<bool> CancelResendAsync(
        Guid registrationId, string leaseId, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(CancelResendScript, [SessionKey(registrationId)], [leaseId], cancellationToken);
        return (long)result == 1;
    }

    private async Task<RedisResult> RunAsync(
        string script, RedisKey[] keys, RedisValue[] values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.ScriptEvaluateAsync(script, keys, values).WaitAsync(cancellationToken);
    }

    private static RegistrationOtpStoreResult Parse(RedisResult raw) => Parse(ArrayResult(raw));

    private static RedisResult[] ArrayResult(RedisResult raw) =>
        (RedisResult[]?)raw ?? throw new InvalidOperationException("Redis returned an unexpected registration OTP result.");

    private static RegistrationOtpStoreResult Parse(RedisResult[] values)
    {
        var status = Value(values[0]);
        return status switch
        {
            "success" => new(RegistrationOtpStoreStatus.Success,
                values.Length == 4 ? Instant(values[1]) : null,
                values.Length == 4 ? Instant(values[2]) : null,
                values.Length == 4 ? Instant(values[3]) : null),
            "pending" => new(RegistrationOtpStoreStatus.PendingRegistration),
            "limited" => new(RegistrationOtpStoreStatus.RateLimited, RetryAfter: Duration(values[1])),
            "unavailable" => new(RegistrationOtpStoreStatus.Unavailable),
            "otpExpired" => new(RegistrationOtpStoreStatus.OtpExpired),
            "invalidOtp" => new(RegistrationOtpStoreStatus.InvalidOtp),
            "attemptsExceeded" => new(RegistrationOtpStoreStatus.AttemptsExceeded),
            "resendCooldown" => new(RegistrationOtpStoreStatus.ResendCooldown, RetryAfter: Duration(values[1])),
            "resendLimitReached" => new(RegistrationOtpStoreStatus.ResendLimitReached),
            "busy" => new(RegistrationOtpStoreStatus.Busy, RetryAfter: Duration(values[1])),
            "alreadyVerified" => new(RegistrationOtpStoreStatus.AlreadyVerified),
            _ => throw new InvalidOperationException($"Unexpected registration OTP store status: {status}")
        };
    }

    private static string Value(RedisResult value) => value.ToString() ?? string.Empty;
    private static DateTimeOffset Instant(RedisResult value) => DateTimeOffset.FromUnixTimeMilliseconds((long)value);
    private static TimeSpan Duration(RedisResult value) => TimeSpan.FromMilliseconds(Math.Max(0, (long)value));
    private static string Id(Guid id) => id.ToString("N");
    private static RedisKey SessionKey(Guid id) => Prefix + "session:" + Id(id);
    private RedisKey EmailKey(string email) => Prefix + "email:" + Hash("email", email);
    private RedisKey RateKey(string kind, string value) => Prefix + "rate:" + kind + ":" + Hash(kind, value);
    private string Hash(string kind, string value) => Convert.ToHexString(
        HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(kind + ":" + value)));

    private static int ReadPositive(IConfiguration section, string name, int fallback)
    {
        var value = section.GetValue(name, fallback);
        if (value <= 0 || value > 35_000)
            throw new InvalidOperationException($"RegistrationOtp:{name} must be positive and within supported range.");
        return value;
    }
}
