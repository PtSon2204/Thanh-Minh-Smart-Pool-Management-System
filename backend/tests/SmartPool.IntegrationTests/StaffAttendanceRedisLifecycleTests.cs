using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.AttendanceQr;
using StackExchange.Redis;

namespace SmartPool.IntegrationTests;

public sealed class StaffAttendanceRedisLifecycleTests
{
    [Fact]
    public async Task GivenRealRedisSession_WhenRotatingExpiringAndStopping_ThenOnlyEligibleActiveTokensResolve()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        using var redis = await ConnectionMultiplexer.ConnectAsync(IntegrationTestSettings.RedisConnection);
        var database = redis.GetDatabase();
        var clock = new SessionClock(new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero));
        var shiftId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var keys = new List<RedisKey>();
        context.Shifts.Add(new Shift
        {
            Id = shiftId, Name = "QA Redis lifecycle", StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0), IsActive = true
        });
        context.EmployeeShifts.Add(new EmployeeShift
        {
            Id = assignmentId, EmployeeId = fixture.AttachedStaffUserId, ShiftId = shiftId,
            WorkDate = new DateOnly(2026, 10, 19), Status = "Scheduled"
        });

        try
        {
            await context.SaveChangesAsync();
            var store = new RedisStaffAttendanceSessionStore(context, redis, clock,
                Options.Create(new StaffAttendanceQrSettings()));
            var issued = await store.CreateAsync(
                new StaffAttendanceQrIssueRequest(shiftId, new DateOnly(2026, 10, 19), StaffAttendanceQrAction.CheckIn),
                fixture.EligibleAdminUserId, default);
            TrackKeys(issued, keys);

            var resolved = await store.ResolveAsync(issued.QrPayload, fixture.AttachedStaffUserId, default);
            Assert.NotNull(resolved);
            Assert.Equal(assignmentId, resolved.AssignmentId);
            Assert.Null(await store.ResolveAsync(issued.QrPayload, fixture.CustomerUserId, default));
            await Assert.ThrowsAsync<StaffAttendanceQrConflictException>(() => store.RotateAsync(
                issued.SessionId, issued.Generation, fixture.EligibleAdminUserId, default));

            clock.Advance(TimeSpan.FromSeconds(60));
            Assert.Null(await store.ResolveAsync(issued.QrPayload, fixture.AttachedStaffUserId, default));
            var rotated = await store.RotateAsync(issued.SessionId, issued.Generation, fixture.EligibleAdminUserId, default);
            TrackKeys(rotated, keys);
            Assert.Equal(2, rotated.Generation);
            Assert.Equal(issued.SessionExpiresAtUtc, rotated.SessionExpiresAtUtc);
            Assert.NotNull(await store.ResolveAsync(rotated.QrPayload, fixture.AttachedStaffUserId, default));
            await Assert.ThrowsAsync<StaffAttendanceQrConflictException>(() => store.RotateAsync(
                issued.SessionId, issued.Generation, fixture.EligibleAdminUserId, default));
            await Assert.ThrowsAsync<StaffAttendanceQrForbiddenException>(() => store.StopAsync(
                issued.SessionId, fixture.AttachedStaffUserId, default));

            await store.StopAsync(issued.SessionId, fixture.EligibleAdminUserId, default);

            Assert.Null(await store.ResolveAsync(rotated.QrPayload, fixture.AttachedStaffUserId, default));
            clock.Advance(TimeSpan.FromMinutes(15));
            await Assert.ThrowsAsync<StaffAttendanceQrConflictException>(() => store.RotateAsync(
                issued.SessionId, rotated.Generation, fixture.EligibleAdminUserId, default));
        }
        finally
        {
            if (keys.Count > 0) await database.KeyDeleteAsync(keys.Distinct().ToArray());
            await context.EmployeeShifts.Where(item => item.Id == assignmentId).ExecuteDeleteAsync();
            await context.Shifts.Where(item => item.Id == shiftId).ExecuteDeleteAsync();
        }
    }

    private static void TrackKeys(StaffAttendanceQrIssuedSession session, List<RedisKey> keys)
    {
        Assert.True(StaffAttendanceQrToken.TryParse(session.QrPayload, out var token));
        var prefix = $"SmartPool:staff-attendance:{{{session.SessionId:N}}}";
        keys.Add($"{prefix}:state");
        keys.Add($"{prefix}:token:{StaffAttendanceQrToken.Digest(token)}");
    }

    private sealed class SessionClock(DateTimeOffset instant) : TimeProvider
    {
        private DateTimeOffset now = instant;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }
}
