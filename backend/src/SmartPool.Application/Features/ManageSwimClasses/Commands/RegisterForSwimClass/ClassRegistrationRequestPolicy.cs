using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;

public static class ClassRegistrationRequestPolicy
{
    public static void EnsureCanRequest(ClassRegistration? registration)
    {
        if (registration is { Status: "Confirmed" })
            throw new SwimClassStateConflictException("Bạn đã được xác nhận tham gia lớp này.");
        if (registration is { Status: "PendingApproval" })
            throw new SwimClassStateConflictException("Yêu cầu tham gia lớp đang chờ Coach duyệt.");
        if (registration is { Status: not ("Rejected" or "Cancelled") })
            throw new SwimClassStateConflictException("Đăng ký lớp hiện tại chưa thể gửi lại.");
    }

    public static void PreparePendingApproval(ClassRegistration registration,
        DateTime registeredAt, DateTime updatedAt)
    {
        registration.Status = "PendingApproval";
        registration.RegisteredAt = registeredAt;
        registration.ConfirmedAt = null;
        registration.CancelledAt = null;
        registration.CancelledByUserId = null;
        registration.CancelReason = null;
        registration.ReviewedByUserId = null;
        registration.ReviewedAt = null;
        registration.ReviewNote = null;
        registration.UpdatedAt = updatedAt;
    }
}
