using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffAttendanceQrAssignmentPolicyTests
{
    [Fact]
    public void GivenPresentAssignmentWithCheckIn_WhenCheckingOut_ThenItIsEligible()
    {
        var assignment = new EmployeeShift
        {
            Status = "Present",
            CheckInTime = new DateTime(2026, 10, 5, 2, 0, 0, DateTimeKind.Utc)
        };

        Assert.True(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckOut));
    }

    [Fact]
    public void GivenAssignmentWithoutAnOpenCheckIn_WhenCheckingOut_ThenItIsIneligible()
    {
        var assignment = new EmployeeShift { Status = "Scheduled" };

        Assert.False(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckOut));
    }

    [Fact]
    public void GivenScheduledAssignmentWithExistingCheckIn_WhenCheckingIn_ThenItIsIneligible()
    {
        var assignment = new EmployeeShift
        {
            Status = "Scheduled",
            CheckInTime = new DateTime(2026, 10, 5, 2, 0, 0, DateTimeKind.Utc)
        };

        Assert.False(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckIn));
    }

    [Fact]
    public void GivenPresentAssignmentWithCompletedCheckOut_WhenCheckingOut_ThenItIsIneligible()
    {
        var assignment = new EmployeeShift
        {
            Status = "Present",
            CheckInTime = new DateTime(2026, 10, 5, 2, 0, 0, DateTimeKind.Utc),
            CheckOutTime = new DateTime(2026, 10, 5, 6, 0, 0, DateTimeKind.Utc)
        };

        Assert.False(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckOut));
    }
}
