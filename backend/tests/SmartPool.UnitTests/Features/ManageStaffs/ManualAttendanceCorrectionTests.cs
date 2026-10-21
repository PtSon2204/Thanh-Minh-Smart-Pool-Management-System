using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class ManualAttendanceCorrectionTests
{
    [Fact]
    public void GivenBlankReason_WhenValidatingManualCorrection_ThenRequestIsRejected()
    {
        var command = ValidCommand();
        command.Reason = "  ";

        Assert.False(new ManualAttendanceCorrectionValidator().Validate(command).IsValid);
    }

    [Fact]
    public void GivenCorrectionTimes_WhenApplyingWorkDateAndFutureRules_ThenSevenDayBoundaryIsInclusive()
    {
        var today = new DateOnly(2026, 10, 19);
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));

        Assert.True(ManualAttendanceCorrectionPolicy.IsWithinLookback(today.AddDays(-7), today, 7));
        Assert.False(ManualAttendanceCorrectionPolicy.IsWithinLookback(today.AddDays(-8), today, 7));
        Assert.False(ManualAttendanceCorrectionPolicy.IsWithinLookback(today.AddDays(1), today, 7));
        Assert.False(ManualAttendanceCorrectionPolicy.IsNotFuture(now.AddSeconds(1), now));
    }

    [Fact]
    public void GivenCheckOutAtOrBeforeCheckIn_WhenValidatingManualCorrection_ThenRequestIsRejected()
    {
        var command = ValidCommand();
        command.CheckOutTime = command.CheckInTime;

        Assert.False(new ManualAttendanceCorrectionValidator().Validate(command).IsValid);
    }

    private static ManualAttendanceCorrectionCommand ValidCommand() => new()
    {
        AssignmentId = Guid.NewGuid(),
        PerformedByUserId = Guid.NewGuid(),
        UpdateCheckIn = true,
        UpdateCheckOut = true,
        CheckInTime = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.FromHours(7)),
        CheckOutTime = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(7)),
        ExpectedUpdatedAtUtc = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero),
        Reason = "Chấm công bổ sung"
    };
}
