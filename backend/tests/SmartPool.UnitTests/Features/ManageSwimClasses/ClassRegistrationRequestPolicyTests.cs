using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class ClassRegistrationRequestPolicyTests
{
    [Fact(DisplayName = "UTCID01 (N) - Chưa từng đăng ký → được tạo yêu cầu mới")]
    public void EnsureCanRequest_NoExistingRegistration_Allows() =>
        ClassRegistrationRequestPolicy.EnsureCanRequest(null);

    [Fact(DisplayName = "UTCID02 (N) - Yêu cầu bị từ chối → được gửi lại")]
    public void EnsureCanRequest_Rejected_Allows() =>
        ClassRegistrationRequestPolicy.EnsureCanRequest(Registration("Rejected"));

    [Fact(DisplayName = "UTCID03 (N) - Yêu cầu đã hủy → được gửi lại")]
    public void EnsureCanRequest_Cancelled_Allows() =>
        ClassRegistrationRequestPolicy.EnsureCanRequest(Registration("Cancelled"));

    [Fact(DisplayName = "UTCID04 (A) - Đã Confirmed → từ chối đăng ký trùng")]
    public void EnsureCanRequest_Confirmed_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationRequestPolicy.EnsureCanRequest(Registration("Confirmed")));
        Assert.Equal("Bạn đã được xác nhận tham gia lớp này.", error.Message);
    }

    [Fact(DisplayName = "UTCID05 (A) - Đang PendingApproval → từ chối yêu cầu trùng")]
    public void EnsureCanRequest_Pending_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationRequestPolicy.EnsureCanRequest(Registration("PendingApproval")));
        Assert.Equal("Yêu cầu tham gia lớp đang chờ Coach duyệt.", error.Message);
    }

    [Fact(DisplayName = "UTCID06 (A) - Trạng thái đăng ký khác không cho gửi lại → xung đột")]
    public void EnsureCanRequest_OtherStatus_Rejects()
    {
        var error = Assert.Throws<SwimClassStateConflictException>(() =>
            ClassRegistrationRequestPolicy.EnsureCanRequest(Registration("Suspended")));
        Assert.Equal("Đăng ký lớp hiện tại chưa thể gửi lại.", error.Message);
    }

    [Fact(DisplayName = "UTCID07 (N) - Gửi lại yêu cầu → PendingApproval và xóa dữ liệu hủy/duyệt cũ")]
    public void PreparePendingApproval_ResetsPreviousOutcome()
    {
        var userId = Guid.NewGuid();
        var registration = Registration("Cancelled");
        registration.ConfirmedAt = DateTime.UtcNow.AddDays(-2);
        registration.CancelledAt = DateTime.UtcNow.AddDays(-1);
        registration.CancelledByUserId = userId;
        registration.CancelReason = "Lịch cũ";
        registration.ReviewedByUserId = userId;
        registration.ReviewedAt = DateTime.UtcNow.AddDays(-2);
        registration.ReviewNote = "Không phù hợp";
        var now = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
        var version = now.AddTicks(1);

        ClassRegistrationRequestPolicy.PreparePendingApproval(registration, now, version);

        Assert.Equal("PendingApproval", registration.Status);
        Assert.Equal(now, registration.RegisteredAt);
        Assert.Equal(version, registration.UpdatedAt);
        Assert.Null(registration.ConfirmedAt);
        Assert.Null(registration.CancelledAt);
        Assert.Null(registration.CancelledByUserId);
        Assert.Null(registration.CancelReason);
        Assert.Null(registration.ReviewedByUserId);
        Assert.Null(registration.ReviewedAt);
        Assert.Null(registration.ReviewNote);
    }

    private static ClassRegistration Registration(string status) => new() { Status = status };
}
