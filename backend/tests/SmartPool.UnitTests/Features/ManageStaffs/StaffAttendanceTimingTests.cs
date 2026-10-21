using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffAttendanceTimingTests
{
    [Fact]
    public void GivenEmptyAssignmentId_WhenValidatingRecordAttendance_ThenValidationFails()
    {
        var result = new RecordAttendanceValidator().Validate(new RecordAttendanceCommand());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GivenManualRecordNow_WhenReasonIsBlank_ThenValidationFails()
    {
        var command = new RecordAttendanceCommand
        {
            Id = Guid.NewGuid(),
            Method = "Manual",
            PerformedByUserId = Guid.NewGuid(),
            Reason = " "
        };

        var result = new RecordAttendanceValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GivenAdminManualCheckIn_WhenRouteAndBodyDisagree_ThenRouteAndClaimAreUsed()
    {
        var routeAssignmentId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        RecordAttendanceCommand? captured = null;
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<RecordAttendanceCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecordAttendanceResponse>, CancellationToken>((request, _) => captured = (RecordAttendanceCommand)request)
            .ReturnsAsync(new RecordAttendanceResponse());
        var controller = new StaffsController(sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, callerId.ToString()), new Claim(ClaimTypes.Role, "ADMIN")], "test"))
                }
            }
        };

        var result = await controller.CheckIn(routeAssignmentId, new RecordAttendanceCommand
        {
            Id = Guid.NewGuid(),
            Reason = "Đúng ca"
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(routeAssignmentId, captured.Id);
        Assert.True(captured.CheckIn);
        Assert.Equal("Manual", captured.Method);
        Assert.Equal(callerId, captured.PerformedByUserId);
        Assert.Equal("Đúng ca", captured.Reason);
    }

    [Fact]
    public void GivenOvernightAssignment_WhenCheckingAttendanceWindows_ThenVietnamBoundariesApply()
    {
        var assignment = new EmployeeShift
        {
            WorkDate = new DateOnly(2026, 10, 6),
            Shift = new Shift { StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(2, 0) },
            CheckInTime = new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc)
        };
        var checkInClock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 14, 40, 0, TimeSpan.Zero));
        var checkOutClock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 19, 0, 0, TimeSpan.Zero));

        Assert.True(StaffAttendanceTiming.CanCheckIn(assignment, checkInClock.GetUtcNow().UtcDateTime, 20));
        Assert.True(StaffAttendanceTiming.CanCheckOut(assignment, checkOutClock.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public void GivenAttendanceWindowBoundaries_WhenCheckingTiming_ThenOnlyInclusiveInstantsAreAllowed()
    {
        var assignment = new EmployeeShift
        {
            WorkDate = new DateOnly(2026, 10, 6),
            Shift = new Shift { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
            CheckInTime = new DateTime(2026, 10, 6, 0, 40, 0, DateTimeKind.Utc)
        };
        var beforeCheckInWindow = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 0, 39, 59, TimeSpan.Zero));
        var checkInWindowStart = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 0, 40, 0, TimeSpan.Zero));
        var scheduledStart = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.Zero));
        var scheduledEnd = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 5, 0, 0, TimeSpan.Zero));
        var beforeCheckOutWindow = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 4, 59, 59, TimeSpan.Zero));

        Assert.False(StaffAttendanceTiming.CanCheckIn(assignment, beforeCheckInWindow.GetUtcNow().UtcDateTime, 20));
        Assert.True(StaffAttendanceTiming.CanCheckIn(assignment, checkInWindowStart.GetUtcNow().UtcDateTime, 20));
        Assert.True(StaffAttendanceTiming.CanCheckIn(assignment, scheduledStart.GetUtcNow().UtcDateTime, 20));
        Assert.False(StaffAttendanceTiming.CanCheckIn(assignment, scheduledEnd.GetUtcNow().UtcDateTime, 20));
        Assert.False(StaffAttendanceTiming.CanCheckOut(assignment, beforeCheckOutWindow.GetUtcNow().UtcDateTime));
        Assert.True(StaffAttendanceTiming.CanCheckOut(assignment, scheduledEnd.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public void GivenNonDefaultCheckInEarlyMinutes_WhenCheckingManualAndQrWindows_ThenBothUseTheSameSetting()
    {
        var settings = new StaffAttendanceQrSettings { CheckInEarlyMinutes = 10 };
        var assignment = new EmployeeShift
        {
            WorkDate = new DateOnly(2026, 10, 6),
            Shift = new Shift { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
        };
        var (start, end) = StaffAttendanceTiming.Interval(assignment.WorkDate, assignment.Shift);
        var elevenMinutesEarly = new DateTime(2026, 10, 6, 0, 49, 0, DateTimeKind.Utc);
        var tenMinutesEarly = new DateTime(2026, 10, 6, 0, 50, 0, DateTimeKind.Utc);

        Assert.False(StaffAttendanceTiming.CanCheckIn(assignment, elevenMinutesEarly, settings.CheckInEarlyMinutes));
        Assert.False(StaffAttendanceQrTiming.CanIssueCheckIn(tenMinutesEarly.AddMinutes(-1), start, end, settings));
        Assert.True(StaffAttendanceTiming.CanCheckIn(assignment, tenMinutesEarly, settings.CheckInEarlyMinutes));
        Assert.True(StaffAttendanceQrTiming.CanIssueCheckIn(tenMinutesEarly, start, end, settings));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
