namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

public static class ClassRegistrationReviewPolicy
{
    public static void EnsurePending(string status, DateTime updatedAt,
        DateTimeOffset expectedUpdatedAt)
    {
        if (updatedAt != expectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Đăng ký đã được cập nhật. Vui lòng tải lại trước khi duyệt.");
        if (status != "PendingApproval")
            throw new SwimClassStateConflictException("Chỉ có thể duyệt yêu cầu đang chờ.");
    }

    public static void EnsureCanAccept(string classStatus, bool hasFutureSession,
        int confirmedCount, int capacity)
    {
        if (classStatus != "Active")
            throw new SwimClassStateConflictException("Lớp đã ngừng nhận học viên.");
        if (!hasFutureSession)
            throw new SwimClassStateConflictException("Lớp không còn buổi học sắp tới.");
        if (confirmedCount >= capacity)
            throw new SwimClassStateConflictException("Lớp đã hết chỗ; yêu cầu vẫn đang chờ duyệt.");
    }
}
