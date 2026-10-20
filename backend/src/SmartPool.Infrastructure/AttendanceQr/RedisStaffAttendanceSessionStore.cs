using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace SmartPool.Infrastructure.AttendanceQr;

public sealed class RedisStaffAttendanceSessionStore : IStaffAttendanceSessionStore
{
    private const string Namespace = "SmartPool:staff-attendance:";
    private const string CreateScript = """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('HSET', KEYS[1], 'issuer', ARGV[1], 'shift', ARGV[2], 'workDate', ARGV[3], 'action', ARGV[4], 'expiresAt', ARGV[5], 'generation', '1', 'lastIssuedAt', ARGV[6], 'stopped', '0', 'actionEndsAt', ARGV[7])
        redis.call('PEXPIRE', KEYS[1], ARGV[8])
        redis.call('SET', KEYS[2], ARGV[9], 'PX', ARGV[10])
        return 1
        """;
    private const string RotateScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 or redis.call('HGET', KEYS[1], 'stopped') ~= '0' then return 0 end
        if tonumber(redis.call('HGET', KEYS[1], 'expiresAt')) <= tonumber(ARGV[1]) then return 0 end
        if tonumber(redis.call('HGET', KEYS[1], 'generation')) ~= tonumber(ARGV[2]) then return 0 end
        if tonumber(redis.call('HGET', KEYS[1], 'lastIssuedAt')) + tonumber(ARGV[3]) > tonumber(ARGV[1]) then return 0 end
        local generation = tonumber(ARGV[2]) + 1
        redis.call('HSET', KEYS[1], 'generation', generation, 'lastIssuedAt', ARGV[1])
        redis.call('SET', KEYS[2], ARGV[4], 'PX', ARGV[5])
        return generation
        """;
    private const string StopScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 or redis.call('HGET', KEYS[1], 'issuer') ~= ARGV[1] then return 0 end
        redis.call('HSET', KEYS[1], 'stopped', '1')
        return 1
        """;
    private const string ResolveScript = """
        local tokenExpiry = redis.call('GET', KEYS[2])
        if not tokenExpiry or tonumber(tokenExpiry) <= tonumber(ARGV[1]) then return nil end
        if redis.call('EXISTS', KEYS[1]) == 0 or redis.call('HGET', KEYS[1], 'stopped') ~= '0' then return nil end
        if tonumber(redis.call('HGET', KEYS[1], 'expiresAt')) <= tonumber(ARGV[1]) then return nil end
        return { redis.call('HGET', KEYS[1], 'issuer'), redis.call('HGET', KEYS[1], 'shift'), redis.call('HGET', KEYS[1], 'workDate'), redis.call('HGET', KEYS[1], 'action'), redis.call('HGET', KEYS[1], 'expiresAt'), tokenExpiry }
        """;

    private readonly SmartPoolDbContext context;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly StaffAttendanceQrSettings settings;

    public RedisStaffAttendanceSessionStore(SmartPoolDbContext context, IConnectionMultiplexer redis, TimeProvider timeProvider,
        IOptions<StaffAttendanceQrSettings> settings)
    {
        this.context = context;
        database = redis.GetDatabase();
        this.timeProvider = timeProvider;
        this.settings = settings.Value;
    }

    public async Task<StaffAttendanceQrIssuedSession> CreateAsync(StaffAttendanceQrIssueRequest request, Guid issuerId, CancellationToken cancellationToken)
    {
        await EnsureActiveAdminAsync(issuerId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var shift = await FindShiftAsync(request.ShiftId, cancellationToken);
        var actionEndsAt = EnsureActionWindow(request.Action, request.WorkDate, shift, now.UtcDateTime);
        if (!await HasEligibleAssignmentAsync(request.ShiftId, request.WorkDate, request.Action, cancellationToken))
            throw new StaffAttendanceQrConflictException("Không có phân công hợp lệ cho mã QR này.");
        var sessionId = Guid.NewGuid();
        var sessionExpiresAt = StaffAttendanceQrTiming.CapSessionExpiry(now, actionEndsAt, settings);
        var tokenExpiresAt = StaffAttendanceQrTiming.CapTokenExpiry(now, sessionExpiresAt, actionEndsAt, settings);
        var token = StaffAttendanceQrToken.Create(sessionId);
        var created = (int)await database.ScriptEvaluateAsync(CreateScript, [SessionKey(sessionId), TokenKey(sessionId, StaffAttendanceQrToken.Digest(token))],
            [issuerId.ToString("N"), request.ShiftId.ToString("N"), request.WorkDate.ToString("yyyyMMdd"), request.Action.ToString(),
                sessionExpiresAt.ToUnixTimeMilliseconds(), now.ToUnixTimeMilliseconds(), actionEndsAt.ToUnixTimeMilliseconds(),
                (long)(sessionExpiresAt - now).TotalMilliseconds, tokenExpiresAt.ToUnixTimeMilliseconds(), (long)(tokenExpiresAt - now).TotalMilliseconds]).WaitAsync(cancellationToken);
        if (created == 0)
            throw new StaffAttendanceQrConflictException("Không thể tạo phiên QR.");
        return new StaffAttendanceQrIssuedSession(sessionId, 1, token, tokenExpiresAt.UtcDateTime, sessionExpiresAt.UtcDateTime);
    }

    public async Task<StaffAttendanceQrIssuedSession> RotateAsync(Guid sessionId, long expectedGeneration, Guid issuerId, CancellationToken cancellationToken)
    {
        await EnsureActiveAdminAsync(issuerId, cancellationToken);
        var session = await ReadSessionAsync(sessionId, cancellationToken);
        if (session.IssuerId != issuerId)
            throw new StaffAttendanceQrForbiddenException();
        var now = timeProvider.GetUtcNow();
        var shift = await FindShiftAsync(session.ShiftId, cancellationToken);
        var actionEndsAt = EnsureActionWindow(session.Action, session.WorkDate, shift, now.UtcDateTime);
        var tokenExpiresAt = StaffAttendanceQrTiming.CapTokenExpiry(now, session.ExpiresAtUtc, actionEndsAt, settings);
        var lifetime = tokenExpiresAt - now;
        if (lifetime <= TimeSpan.Zero)
            throw new StaffAttendanceQrConflictException("Phiên QR đã hết hạn.");
        var token = StaffAttendanceQrToken.Create(sessionId);
        var generation = (long)await database.ScriptEvaluateAsync(RotateScript, [SessionKey(sessionId), TokenKey(sessionId, StaffAttendanceQrToken.Digest(token))],
            [now.ToUnixTimeMilliseconds(), expectedGeneration, settings.RotationIntervalSeconds * 1000L, tokenExpiresAt.ToUnixTimeMilliseconds(), (long)lifetime.TotalMilliseconds]).WaitAsync(cancellationToken);
        if (generation == 0)
            throw new StaffAttendanceQrConflictException("Phiên QR đã dừng, hết hạn hoặc phiên bản không còn mới.");
        return new StaffAttendanceQrIssuedSession(sessionId, generation, token, tokenExpiresAt.UtcDateTime, session.ExpiresAtUtc.UtcDateTime);
    }

    public async Task StopAsync(Guid sessionId, Guid issuerId, CancellationToken cancellationToken)
    {
        await EnsureActiveAdminAsync(issuerId, cancellationToken);
        var stopped = (int)await database.ScriptEvaluateAsync(StopScript, [SessionKey(sessionId)], [issuerId.ToString("N")]).WaitAsync(cancellationToken);
        if (stopped == 0)
            throw new StaffAttendanceQrForbiddenException();
    }

    public async Task<StaffAttendanceQrResolution?> ResolveAsync(string qrPayload, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!StaffAttendanceQrToken.TryParse(qrPayload, out var token) || !StaffAttendanceQrToken.TryGetSessionId(token, out var sessionId))
            return null;
        var now = timeProvider.GetUtcNow();
        var response = await database.ScriptEvaluateAsync(ResolveScript, [SessionKey(sessionId), TokenKey(sessionId, StaffAttendanceQrToken.Digest(token))], [now.ToUnixTimeMilliseconds()]).WaitAsync(cancellationToken);
        if (response.IsNull)
            return null;
        var result = (RedisResult[]?)response;
        if (result is not { Length: 6 } || !Guid.TryParse(result[0].ToString(), out var issuerId) || !Guid.TryParse(result[1].ToString(), out var shiftId) ||
            !DateOnly.TryParseExact(result[2].ToString(), "yyyyMMdd", out var workDate) || !Enum.TryParse<StaffAttendanceQrAction>(result[3].ToString(), out var action) ||
            !long.TryParse(result[4].ToString(), out var sessionExpiry) || !long.TryParse(result[5].ToString(), out var tokenExpiry))
            return null;
        if (!await IsActiveAdminAsync(issuerId, cancellationToken))
            return null;
        var assignment = await context.EmployeeShifts.AsNoTracking().Include(item => item.Shift).Include(item => item.Employee).ThenInclude(item => item.User)
            .FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.ShiftId == shiftId && item.WorkDate == workDate &&
                item.Employee.Status == "Working" && item.Employee.User.IsDeleted != true && item.Employee.User.Status != null &&
                item.Employee.User.Status.ToUpper() == "ACTIVE" && item.Employee.User.Role != null && item.Employee.User.Role.IsDeleted != true && item.Employee.User.Role.Name != null &&
                (item.Employee.User.Role.Name.ToUpper() == nameof(RoleEnum.SALES) || item.Employee.User.Role.Name.ToUpper() == nameof(RoleEnum.LIFEGUARD)), cancellationToken);
        if (assignment is null || !StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, action))
            return null;
        var actionEndsAt = EnsureActionWindow(action, workDate, assignment.Shift, now.UtcDateTime);
        if (now >= DateTimeOffset.FromUnixTimeMilliseconds(tokenExpiry) || now >= actionEndsAt)
            return null;
        return new StaffAttendanceQrResolution(assignment.Id, sessionId, shiftId, workDate, action,
            DateTimeOffset.FromUnixTimeMilliseconds(tokenExpiry).UtcDateTime, DateTimeOffset.FromUnixTimeMilliseconds(sessionExpiry).UtcDateTime);
    }

    private async Task<SmartPool.Domain.Entities.Shift> FindShiftAsync(Guid shiftId, CancellationToken cancellationToken) =>
        await context.Shifts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken)
        ?? throw new StaffAttendanceQrConflictException("Không tìm thấy ca làm việc.");

    private async Task<bool> HasEligibleAssignmentAsync(Guid shiftId, DateOnly workDate, StaffAttendanceQrAction action, CancellationToken cancellationToken) =>
        await context.EmployeeShifts.AsNoTracking().AnyAsync(item => item.ShiftId == shiftId && item.WorkDate == workDate &&
            item.Employee.Status == "Working" && item.Employee.User.IsDeleted != true && item.Employee.User.Status != null && item.Employee.User.Status.ToUpper() == "ACTIVE" &&
            item.Employee.User.Role != null && item.Employee.User.Role.IsDeleted != true && item.Employee.User.Role.Name != null &&
            (item.Employee.User.Role.Name.ToUpper() == nameof(RoleEnum.SALES) || item.Employee.User.Role.Name.ToUpper() == nameof(RoleEnum.LIFEGUARD)) &&
            (action == StaffAttendanceQrAction.CheckIn ? item.Status == "Scheduled" && item.CheckInTime == null : item.Status == "Present" && item.CheckInTime != null && item.CheckOutTime == null), cancellationToken);

    private DateTimeOffset EnsureActionWindow(StaffAttendanceQrAction action, DateOnly workDate, SmartPool.Domain.Entities.Shift shift, DateTime now)
    {
        var (start, end) = StaffAttendanceTiming.Interval(workDate, shift);
        var actionEndsAt = action == StaffAttendanceQrAction.CheckIn ? new DateTimeOffset(end) : new DateTimeOffset(end.AddDays(settings.CheckoutWindowDays));
        var valid = action == StaffAttendanceQrAction.CheckIn ? StaffAttendanceQrTiming.CanIssueCheckIn(now, start, end, settings) : now >= end && now < actionEndsAt.UtcDateTime;
        if (!valid)
            throw new StaffAttendanceQrConflictException("Hiện không trong thời gian chấm công QR cho ca này.");
        return actionEndsAt;
    }

    private async Task<(Guid IssuerId, Guid ShiftId, DateOnly WorkDate, StaffAttendanceQrAction Action, DateTimeOffset ExpiresAtUtc)> ReadSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var values = await database.HashGetAsync(SessionKey(sessionId), ["issuer", "shift", "workDate", "action", "expiresAt", "stopped"]).WaitAsync(cancellationToken);
        if (values.Length != 6 || values.Any(value => value.IsNull) || values[5] != "0" || !Guid.TryParse(values[0], out var issuerId) ||
            !Guid.TryParse(values[1], out var shiftId) || !DateOnly.TryParseExact(values[2], "yyyyMMdd", out var workDate) ||
            !Enum.TryParse<StaffAttendanceQrAction>(values[3], out var action) || !long.TryParse(values[4], out var expiry))
            throw new StaffAttendanceQrConflictException("Phiên QR không còn hoạt động.");
        return (issuerId, shiftId, workDate, action, DateTimeOffset.FromUnixTimeMilliseconds(expiry));
    }

    private async Task EnsureActiveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsActiveAdminAsync(userId, cancellationToken))
            throw new StaffAttendanceQrForbiddenException();
    }

    private Task<bool> IsActiveAdminAsync(Guid userId, CancellationToken cancellationToken) => context.Users.AsNoTracking().AnyAsync(user =>
        user.Id == userId && user.IsDeleted != true && user.Status != null && user.Status.ToUpper() == "ACTIVE" && user.Role != null &&
        user.Role.IsDeleted != true && user.Role.Name.ToUpper() == nameof(RoleEnum.ADMIN), cancellationToken);

    private static RedisKey SessionKey(Guid sessionId) => $"{Namespace}{{{sessionId:N}}}:state";
    private static RedisKey TokenKey(Guid sessionId, string digest) => $"{Namespace}{{{sessionId:N}}}:token:{digest}";
}
