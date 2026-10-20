using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffAttendanceIntegrationTests
{
    [Fact]
    public async Task CheckInAndCheckOutUseServerTimeAndCannotBeRepeated()
    {
        await using var fixture = await AttendanceFixture.CreateAsync();
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(false));
        await fixture.AttendAsync(true);
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(true));
        fixture.Clock.Now = fixture.Clock.Now.AddHours(3);
        await fixture.AttendAsync(false);
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(false));
        await using var read = fixture.Employment.CreateContext();
        var assignment = await read.EmployeeShifts.SingleAsync(s => s.Id == fixture.AssignmentId);
        Assert.Equal("Completed", assignment.Status);
        Assert.Equal(new DateTime(2026, 10, 19, 2, 0, 0, DateTimeKind.Utc), assignment.CheckInTime);
        Assert.Equal(fixture.Clock.Now.UtcDateTime, assignment.CheckOutTime);
    }

    [Fact]
    public async Task ForeignEmployeeAndInactiveEmploymentCannotCheckIn()
    {
        await using var fixture = await AttendanceFixture.CreateAsync();
        await Assert.ThrowsAsync<StaffForbiddenException>(() => fixture.AttendAsync(true, Guid.NewGuid()));
        await using (var context = fixture.Employment.CreateContext())
            await context.Employees.Where(e => e.UserId == fixture.EmployeeId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Inactive"));
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(true));
    }

    [Fact]
    public async Task ConcurrentCheckInsHaveOneWinner()
    {
        await using var fixture = await AttendanceFixture.CreateAsync();
        async Task<bool> TryAttend()
        {
            try { await fixture.AttendAsync(true); return true; }
            catch (StaffConflictException) { return false; }
        }
        var results = await Task.WhenAll(TryAttend(), TryAttend());
        Assert.Equal(1, results.Count(success => success));
    }

    [Fact]
    public async Task FutureAndCancelledAssignmentsCannotCheckIn()
    {
        await using var fixture = await AttendanceFixture.CreateAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(-1);
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(true));
        fixture.Clock.Now = fixture.Clock.Now.AddDays(1);
        await using (var context = fixture.Employment.CreateContext())
            await context.EmployeeShifts.Where(s => s.Id == fixture.AssignmentId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Cancelled"));
        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.AttendAsync(true));
    }

    [Fact]
    public async Task OvernightShiftCanCheckInAfterMidnightAndCheckOutOnFollowingDate()
    {
        await using var fixture = await AttendanceFixture.CreateAsync(overnight: true);
        // 02:00 on Oct 20 in Vietnam, for the shift starting Oct 19 at 22:00.
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 19, 19, 0, 0, TimeSpan.Zero);
        await fixture.AttendAsync(true);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(4);
        await fixture.AttendAsync(false);
        await using var read = fixture.Employment.CreateContext();
        Assert.Equal("Completed", await read.EmployeeShifts.Where(s => s.Id == fixture.AssignmentId).Select(s => s.Status).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseRejectsCheckoutWithoutCheckinOrBeforeCheckin(bool earlierCheckout)
    {
        await using var fixture = await AttendanceFixture.CreateAsync();
        await using var context = fixture.Employment.CreateContext();
        DateTime? checkIn = earlierCheckout ? fixture.Clock.Now.UtcDateTime : null;
        var checkOut = fixture.Clock.Now.UtcDateTime.AddHours(-1);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.employee_shifts SET check_in_time = {checkIn}, check_out_time = {checkOut} WHERE id = {fixture.AssignmentId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 2, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class AttendanceFixture(StaffEmploymentFixture employment) : IAsyncDisposable
    {
        public StaffEmploymentFixture Employment { get; } = employment;
        public Guid EmployeeId => Employment.AttachedStaffUserId;
        public Guid AssignmentId { get; } = Guid.NewGuid();
        private Guid ShiftId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public static async Task<AttendanceFixture> CreateAsync(bool overnight = false)
        {
            var fixture = new AttendanceFixture(await StaffEmploymentFixture.CreateAsync());
            await using var context = fixture.Employment.CreateContext();
            context.Shifts.Add(new Shift { Id = fixture.ShiftId, Name = "Attendance test", StartTime = new TimeOnly(overnight ? 22 : 8, 0), EndTime = new TimeOnly(overnight ? 6 : 17, 0), IsActive = true });
            context.EmployeeShifts.Add(new EmployeeShift { Id = fixture.AssignmentId, EmployeeId = fixture.EmployeeId, ShiftId = fixture.ShiftId, WorkDate = new DateOnly(2026, 10, 19), Status = "Scheduled" });
            await context.SaveChangesAsync();
            return fixture;
        }
        public async Task AttendAsync(bool checkIn, Guid? ownerId = null)
        {
            await using var context = Employment.CreateContext();
            await Employment.CreateOperations(context, Clock).RecordAttendanceAsync(new RecordAttendanceCommand { Id = AssignmentId, CheckIn = checkIn, OwnerId = ownerId ?? EmployeeId }, default);
        }
        public async ValueTask DisposeAsync()
        {
            await using (var context = Employment.CreateContext())
            {
                await context.EmployeeShifts.Where(s => s.Id == AssignmentId).ExecuteDeleteAsync();
                await context.Shifts.Where(s => s.Id == ShiftId).ExecuteDeleteAsync();
            }
            await Employment.DisposeAsync();
        }
    }
}
