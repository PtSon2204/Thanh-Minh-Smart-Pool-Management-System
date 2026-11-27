using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CompleteSwimClass;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassCompletionPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FinishedAndFullyMarkedClassCanClose() =>
        ClassCompletionPolicy.EnsureCanComplete("Active", [Now.AddMinutes(-1)], false, false, Now);

    [Theory]
    [InlineData("Completed", -1, false, false)]
    [InlineData("Active", 1, false, false)]
    [InlineData("Active", -1, true, false)]
    [InlineData("Active", -1, false, true)]
    public void ClassCannotCloseWhileNotReady(string status, int minutesFromNow,
        bool pendingRegistration, bool missingAttendance) =>
        Assert.Throws<SwimClassStateConflictException>(() =>
            ClassCompletionPolicy.EnsureCanComplete(status, [Now.AddMinutes(minutesFromNow)],
                pendingRegistration, missingAttendance, Now));

    [Fact]
    public void ClassWithoutScheduledSessionsCannotClose() =>
        Assert.Throws<SwimClassStateConflictException>(() =>
            ClassCompletionPolicy.EnsureCanComplete("Active", [], false, false, Now));
}
