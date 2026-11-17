using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassAttendanceReviewPolicyTests
{
    private static readonly DateTime Version = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset ExpectedVersion = new(Version);

    [Fact(DisplayName = "UTCID01 (N) - Coach phụ trách buổi → được review")]
    public void EnsureAssignedCoach_Assigned_Allows()
    {
        var coachId = Guid.NewGuid();
        ClassAttendanceReviewPolicy.EnsureAssignedCoach(coachId, coachId);
    }

    [Fact(DisplayName = "UTCID02 (A) - Coach khác buổi → từ chối review")]
    public void EnsureAssignedCoach_NotAssigned_Rejects()
    {
        var error = Assert.Throws<UnauthorizedAccessException>(() =>
            ClassAttendanceReviewPolicy.EnsureAssignedCoach(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal("Chỉ Coach phụ trách buổi được duyệt điểm danh.", error.Message);
    }

    [Fact(DisplayName = "UTCID03 (N) - Scheduled, PendingReview và phiên bản đúng → được review")]
    public void EnsureReviewable_PendingCurrent_Allows() =>
        ClassAttendanceReviewPolicy.EnsureReviewable("Scheduled", "PendingReview", Version, ExpectedVersion);

    [Fact(DisplayName = "UTCID04 (A) - Phiên bản điểm danh đã cũ → yêu cầu tải lại")]
    public void EnsureReviewable_StaleVersion_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceReviewPolicy.EnsureReviewable("Scheduled", "PendingReview", Version.AddTicks(1), ExpectedVersion));
        Assert.Contains("tải lại", error.Message);
    }

    [Fact(DisplayName = "UTCID05 (A) - Điểm danh đã Verified/Rejected → không review lại")]
    public void EnsureReviewable_AlreadyReviewed_Rejects()
    {
        foreach (var status in new[] { "Verified", "Rejected" })
        {
            var error = Assert.Throws<SwimClassStateConflictException>(() =>
                ClassAttendanceReviewPolicy.EnsureReviewable("Scheduled", status, Version, ExpectedVersion));
            Assert.Equal("Chỉ có thể duyệt điểm danh đang chờ xem xét.", error.Message);
        }
    }

    [Fact(DisplayName = "UTCID06 (A) - Buổi đã Cancelled → không review điểm danh")]
    public void EnsureReviewable_CancelledSession_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassAttendanceReviewPolicy.EnsureReviewable("Cancelled", "PendingReview", Version, ExpectedVersion));
        Assert.Equal("Không thể duyệt điểm danh của buổi đã hủy.", error.Message);
    }
}
