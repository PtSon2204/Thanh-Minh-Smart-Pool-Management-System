using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassRegistrationReviewPolicyTests
{
    private static readonly DateTime Version = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset ExpectedVersion = new(Version);

    [Fact(DisplayName = "UTCID01 (N) - PendingApproval và phiên bản đúng → được review")]
    public void EnsurePending_CurrentPending_Allows() =>
        ClassRegistrationReviewPolicy.EnsurePending("PendingApproval", Version, ExpectedVersion);

    [Fact(DisplayName = "UTCID02 (A) - Phiên bản đăng ký đã cũ → xung đột và yêu cầu tải lại")]
    public void EnsurePending_StaleVersion_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationReviewPolicy.EnsurePending("PendingApproval", Version.AddTicks(1), ExpectedVersion));
        Assert.Contains("tải lại", error.Message);
    }

    [Fact(DisplayName = "UTCID03 (A) - Đăng ký đã Confirmed → không review lại")]
    public void EnsurePending_Confirmed_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationReviewPolicy.EnsurePending("Confirmed", Version, ExpectedVersion));
        Assert.Equal("Chỉ có thể duyệt yêu cầu đang chờ.", error.Message);
    }

    [Fact(DisplayName = "UTCID04 (N) - Lớp Active, có buổi tương lai và còn chỗ → được chấp nhận")]
    public void EnsureCanAccept_EligibleClass_Allows() =>
        ClassRegistrationReviewPolicy.EnsureCanAccept("Active", true, 2, 5);

    [Fact(DisplayName = "UTCID05 (A) - Lớp Draft/Inactive → không chấp nhận")]
    public void EnsureCanAccept_ClassNotActive_Rejects()
    {
        foreach (var status in new[] { "Draft", "Inactive" })
        {
            var error = Assert.Throws<SwimClassStateConflictException>(() =>
                ClassRegistrationReviewPolicy.EnsureCanAccept(status, true, 0, 5));
            Assert.Equal("Lớp đã ngừng nhận học viên.", error.Message);
        }
    }

    [Fact(DisplayName = "UTCID06 (A) - Lớp không còn buổi học sắp tới → không chấp nhận")]
    public void EnsureCanAccept_NoFutureSession_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationReviewPolicy.EnsureCanAccept("Active", false, 0, 5));
        Assert.Equal("Lớp không còn buổi học sắp tới.", error.Message);
    }

    [Fact(DisplayName = "UTCID07 (B) - Số Confirmed bằng sức chứa → hết chỗ, giữ yêu cầu chờ")]
    public void EnsureCanAccept_AtCapacity_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationReviewPolicy.EnsureCanAccept("Active", true, 5, 5));
        Assert.Equal("Lớp đã hết chỗ; yêu cầu vẫn đang chờ duyệt.", error.Message);
    }

    [Fact(DisplayName = "UTCID08 (B) - Còn đúng một chỗ → được chấp nhận")]
    public void EnsureCanAccept_OnePlaceLeft_Allows() =>
        ClassRegistrationReviewPolicy.EnsureCanAccept("Active", true, 4, 5);
}
