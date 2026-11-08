namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

public static class ClassAttendanceReviewPolicy
{
    public static void EnsureAssignedCoach(Guid assignedCoachUserId, Guid reviewerUserId)
    {
        if (assignedCoachUserId != reviewerUserId)
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được duyệt điểm danh.");
    }

    public static void EnsureReviewable(string sessionStatus, string attendanceStatus,
        DateTime updatedAt, DateTimeOffset expectedUpdatedAt)
    {
        if (updatedAt != expectedUpdatedAt.UtcDateTime)
            throw new SwimClassStateConflictException(
                "Điểm danh đã được cập nhật. Vui lòng tải lại trước khi duyệt.");
        if (attendanceStatus != "PendingReview")
            throw new SwimClassStateConflictException("Chỉ có thể duyệt điểm danh đang chờ xem xét.");
        if (sessionStatus != "Scheduled")
            throw new SwimClassStateConflictException("Không thể duyệt điểm danh của buổi đã hủy.");
    }
}
