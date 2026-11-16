using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassAttendanceSubmissionPolicyTests
{
    private static readonly DateTime StartsAt = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndsAt = StartsAt.AddHours(1);

    [Fact(DisplayName = "UTCID01 (N) - Confirmed, Scheduled, chưa nộp và đang trong hạn → được điểm danh")]
    public void EnsureAllowed_ValidContext_Allows() =>
        ClassAttendanceSubmissionPolicy.EnsureAllowed(Context(), StartsAt.AddMinutes(30));

    [Fact(DisplayName = "UTCID02 (B) - Đúng lúc bắt đầu buổi → còn trong hạn nộp")]
    public void IsWithinWindow_AtStart_IsTrue() =>
        Assert.True(ClassAttendanceSubmissionPolicy.IsWithinWindow(StartsAt, EndsAt, StartsAt));

    [Fact(DisplayName = "UTCID03 (B) - Đúng 24 giờ sau khi kết thúc → còn trong hạn nộp")]
    public void IsWithinWindow_AtDeadline_IsTrue() =>
        Assert.True(ClassAttendanceSubmissionPolicy.IsWithinWindow(StartsAt, EndsAt, EndsAt.AddHours(24)));

    [Fact(DisplayName = "UTCID04 (B) - Trước lúc bắt đầu → chưa được nộp")]
    public void EnsureAllowed_BeforeStart_Rejects() =>
        AssertWindowConflict(StartsAt.AddTicks(-1));

    [Fact(DisplayName = "UTCID05 (B) - Quá 24 giờ sau khi kết thúc → hết hạn nộp")]
    public void EnsureAllowed_AfterDeadline_Rejects() =>
        AssertWindowConflict(EndsAt.AddHours(24).AddTicks(1));

    [Fact(DisplayName = "UTCID06 (A) - Đã nộp ảnh → không được nộp lần hai")]
    public void EnsureAllowed_AlreadySubmitted_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceSubmissionPolicy.EnsureAllowed(Context(alreadySubmitted: true), StartsAt));
        Assert.Equal("Bạn đã nộp ảnh điểm danh cho buổi học này.", error.Message);
    }

    [Fact(DisplayName = "UTCID07 (A) - Đăng ký chưa Confirmed → không được nộp")]
    public void EnsureAllowed_PendingRegistration_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceSubmissionPolicy.EnsureAllowed(Context(registrationStatus: "PendingApproval"), StartsAt));
        Assert.Equal("Chỉ đăng ký Confirmed được nộp ảnh điểm danh.", error.Message);
    }

    [Fact(DisplayName = "UTCID08 (A) - Buổi đã Cancelled → không được nộp")]
    public void EnsureAllowed_CancelledSession_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceSubmissionPolicy.EnsureAllowed(Context(sessionStatus: "Cancelled"), StartsAt));
        Assert.Equal("Buổi học đã hủy, không thể nộp ảnh điểm danh.", error.Message);
    }

    private static void AssertWindowConflict(DateTime now)
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceSubmissionPolicy.EnsureAllowed(Context(), now));
        Assert.Equal("Buổi học chưa bắt đầu hoặc đã quá hạn nộp ảnh điểm danh.", error.Message);
    }

    private static AttendanceSubmissionContext Context(string registrationStatus = "Confirmed",
        string sessionStatus = "Scheduled", bool alreadySubmitted = false) =>
        new(Guid.NewGuid(), registrationStatus, sessionStatus, StartsAt, EndsAt, alreadySubmitted);
}
