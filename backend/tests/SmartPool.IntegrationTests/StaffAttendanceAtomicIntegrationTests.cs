using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffAttendanceAtomicIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly WorkDate = new(2026, 10, 19);

    [Fact]
    public async Task GivenManualCheckInOutsideTheActionWindow_WhenRecording_ThenStateAndHistoryStayUnchanged()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Scheduled", null, null, null, null,
                shiftStart: new TimeOnly(8, 0), shiftEnd: new TimeOnly(10, 0));
            await using (var operationContext = fixture.CreateContext())
            {
                var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
                await Assert.ThrowsAsync<StaffConflictException>(() => operations.RecordAttendanceAsync(new RecordAttendanceCommand
                {
                    Id = assignmentId,
                    CheckIn = true,
                    Method = "Manual",
                    PerformedByUserId = fixture.EligibleAdminUserId,
                    Reason = "Outside attendance window"
                }, CancellationToken.None));
            }

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            Assert.Equal("Scheduled", assignment.Status);
            Assert.Null(assignment.CheckInTime);
            Assert.Null(assignment.CheckInMethod);
            Assert.Empty(await verifyContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    [Fact]
    public async Task GivenManualRecordNowAuditActorMissing_WhenSavingAuditFails_ThenAttendanceUpdateRollsBack()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Scheduled", null, null, null, null);
            await using (var operationContext = fixture.CreateContext())
            {
                var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
                await Assert.ThrowsAsync<DbUpdateException>(() => operations.RecordAttendanceAsync(new RecordAttendanceCommand
                {
                    Id = assignmentId,
                    CheckIn = true,
                    Method = "Manual",
                    PerformedByUserId = Guid.NewGuid(),
                    Reason = "Integration test audit rollback"
                }, CancellationToken.None));
            }

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            Assert.Equal("Scheduled", assignment.Status);
            Assert.Null(assignment.CheckInTime);
            Assert.Null(assignment.CheckInMethod);
            Assert.Empty(await verifyContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    [Fact]
    public async Task GivenCorrectionAuditActorMissing_WhenSavingAuditFails_ThenTimesMethodsAndStatusRollBack()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var checkIn = Now.AddHours(-2).UtcDateTime;
        var expectedUpdatedAt = Now.AddMinutes(-5);

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Present", checkIn, null, "QR", null, expectedUpdatedAt.UtcDateTime);
            await using (var operationContext = fixture.CreateContext())
            {
                var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
                await Assert.ThrowsAsync<DbUpdateException>(() => operations.CorrectAttendanceAsync(new ManualAttendanceCorrectionCommand
                {
                    AssignmentId = assignmentId,
                    PerformedByUserId = Guid.NewGuid(),
                    UpdateCheckOut = true,
                    CheckOutTime = Now.AddHours(-1),
                    ExpectedUpdatedAtUtc = expectedUpdatedAt,
                    Reason = "Integration test audit rollback"
                }, CancellationToken.None));
            }

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            Assert.Equal("Present", assignment.Status);
            Assert.Equal(checkIn, assignment.CheckInTime);
            Assert.Equal("QR", assignment.CheckInMethod);
            Assert.Null(assignment.CheckOutTime);
            Assert.Null(assignment.CheckOutMethod);
            Assert.Equal(expectedUpdatedAt.UtcDateTime, assignment.UpdatedAt);
            Assert.Empty(await verifyContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    [Fact]
    public async Task GivenTwoCorrectionsWithSameSnapshot_WhenTheyRunConcurrently_ThenOnlyOneWritesAnAudit()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var checkIn = Now.AddHours(-2).UtcDateTime;
        var expectedUpdatedAt = Now.AddMinutes(-5);
        var checkoutChoices = new[] { Now.AddHours(-1), Now.AddMinutes(-30) };

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Present", checkIn, null, "QR", null, expectedUpdatedAt.UtcDateTime);
            var outcomes = await Task.WhenAll(checkoutChoices.Select(checkout => TryCorrectAsync(
                fixture, assignmentId, expectedUpdatedAt, checkout, fixture.EligibleAdminUserId)));

            Assert.Single(outcomes, outcome => outcome);
            Assert.Single(outcomes, outcome => !outcome);

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            Assert.Equal("Completed", assignment.Status);
            Assert.Contains(checkoutChoices, value => assignment.CheckOutTime == value.UtcDateTime);
            Assert.Equal("QR", assignment.CheckInMethod);
            Assert.Equal("Manual", assignment.CheckOutMethod);
            Assert.Single(await verifyContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    [Fact]
    public async Task GivenCompletedAttendance_WhenCorrectingBothTimes_ThenCompletedStatusAndBeforeAfterHistoryAreSaved()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var previousCheckIn = Now.AddHours(-2).UtcDateTime;
        var previousCheckOut = Now.AddHours(-1).UtcDateTime;
        var expectedUpdatedAt = Now.AddMinutes(-5);
        var correctedCheckIn = Now.AddHours(-2).AddMinutes(10);
        var correctedCheckOut = Now.AddHours(-1).AddMinutes(10);

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Completed", previousCheckIn, previousCheckOut, "QR", "QR", expectedUpdatedAt.UtcDateTime);
            await using (var operationContext = fixture.CreateContext())
            {
                var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
                await operations.CorrectAttendanceAsync(new ManualAttendanceCorrectionCommand
                {
                    AssignmentId = assignmentId,
                    PerformedByUserId = fixture.EligibleAdminUserId,
                    UpdateCheckIn = true,
                    CheckInTime = correctedCheckIn,
                    UpdateCheckOut = true,
                    CheckOutTime = correctedCheckOut,
                    ExpectedUpdatedAtUtc = expectedUpdatedAt,
                    Reason = "Completed shift correction"
                }, CancellationToken.None);
            }

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            var audit = await verifyContext.AttendanceLogs.AsNoTracking().SingleAsync(item => item.EmployeeShiftId == assignmentId);
            Assert.Equal("Completed", assignment.Status);
            Assert.Equal(correctedCheckIn.UtcDateTime, assignment.CheckInTime);
            Assert.Equal(correctedCheckOut.UtcDateTime, assignment.CheckOutTime);
            Assert.Equal("Manual", assignment.CheckInMethod);
            Assert.Equal("Manual", assignment.CheckOutMethod);
            Assert.Equal("Completed", audit.BeforeStatus);
            Assert.Equal("Completed", audit.AfterStatus);
            Assert.Equal(previousCheckIn, audit.BeforeCheckInTime);
            Assert.Equal(previousCheckOut, audit.BeforeCheckOutTime);
            Assert.Equal("QR", audit.BeforeCheckInMethod);
            Assert.Equal("QR", audit.BeforeCheckOutMethod);
            Assert.Equal(correctedCheckIn.UtcDateTime, audit.AfterCheckInTime);
            Assert.Equal(correctedCheckOut.UtcDateTime, audit.AfterCheckOutTime);
            Assert.Equal("Manual", audit.AfterCheckInMethod);
            Assert.Equal("Manual", audit.AfterCheckOutMethod);
            Assert.Equal("Completed shift correction", audit.Reason);
            Assert.Equal(fixture.EligibleAdminUserId, audit.PerformedByUserId);
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    [Fact]
    public async Task GivenCancelledAssignment_WhenCorrectingAttendance_ThenStateAndHistoryRemainUnchanged()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var assignmentId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var expectedUpdatedAt = Now.AddMinutes(-5);

        try
        {
            await SeedAssignmentAsync(fixture, assignmentId, shiftId, "Cancelled", null, null, null, null, expectedUpdatedAt.UtcDateTime);
            await using (var operationContext = fixture.CreateContext())
            {
                var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
                await Assert.ThrowsAsync<StaffConflictException>(() => operations.CorrectAttendanceAsync(new ManualAttendanceCorrectionCommand
                {
                    AssignmentId = assignmentId,
                    PerformedByUserId = fixture.EligibleAdminUserId,
                    UpdateCheckIn = true,
                    CheckInTime = Now.AddHours(-1),
                    ExpectedUpdatedAtUtc = expectedUpdatedAt,
                    Reason = "Cancelled shift correction must be rejected"
                }, CancellationToken.None));
            }

            await using var verifyContext = fixture.CreateContext();
            var assignment = await verifyContext.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == assignmentId);
            Assert.Equal("Cancelled", assignment.Status);
            Assert.Null(assignment.CheckInTime);
            Assert.Null(assignment.CheckOutTime);
            Assert.Empty(await verifyContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(fixture, assignmentId, shiftId);
        }
    }

    private static async Task<bool> TryCorrectAsync(StaffEmploymentFixture fixture, Guid assignmentId,
        DateTimeOffset expectedUpdatedAt, DateTimeOffset checkOut, Guid actorId)
    {
        await using var operationContext = fixture.CreateContext();
        var operations = fixture.CreateOperations(operationContext, new FixedTimeProvider(Now));
        try
        {
            await operations.CorrectAttendanceAsync(new ManualAttendanceCorrectionCommand
            {
                AssignmentId = assignmentId,
                PerformedByUserId = actorId,
                UpdateCheckOut = true,
                CheckOutTime = checkOut,
                ExpectedUpdatedAtUtc = expectedUpdatedAt,
                Reason = "Integration test concurrent correction"
            }, CancellationToken.None);
            return true;
        }
        catch (StaffConflictException)
        {
            return false;
        }
    }

    private static async Task SeedAssignmentAsync(StaffEmploymentFixture fixture, Guid assignmentId, Guid shiftId,
        string status, DateTime? checkIn, DateTime? checkOut, string? checkInMethod, string? checkOutMethod,
        DateTime? updatedAt = null, TimeOnly? shiftStart = null, TimeOnly? shiftEnd = null)
    {
        await using var setupContext = fixture.CreateContext();
        setupContext.Shifts.Add(new Shift
        {
            Id = shiftId,
            Name = "Attendance integration test",
            StartTime = shiftStart ?? new TimeOnly(10, 0),
            EndTime = shiftEnd ?? new TimeOnly(12, 0),
            IsActive = true
        });
        setupContext.EmployeeShifts.Add(new EmployeeShift
        {
            Id = assignmentId,
            EmployeeId = fixture.AttachedStaffUserId,
            ShiftId = shiftId,
            WorkDate = WorkDate,
            Status = status,
            CheckInTime = checkIn,
            CheckOutTime = checkOut,
            CheckInMethod = checkInMethod,
            CheckOutMethod = checkOutMethod,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = updatedAt ?? Now.AddMinutes(-5).UtcDateTime
        });
        await setupContext.SaveChangesAsync();
    }

    private static async Task CleanupAsync(StaffEmploymentFixture fixture, Guid assignmentId, Guid shiftId)
    {
        await using var cleanupContext = fixture.CreateContext();
        await cleanupContext.AttendanceLogs.Where(item => item.EmployeeShiftId == assignmentId).ExecuteDeleteAsync();
        await cleanupContext.EmployeeShifts.Where(item => item.Id == assignmentId).ExecuteDeleteAsync();
        await cleanupContext.Shifts.Where(item => item.Id == shiftId).ExecuteDeleteAsync();
        await fixture.CleanupAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
