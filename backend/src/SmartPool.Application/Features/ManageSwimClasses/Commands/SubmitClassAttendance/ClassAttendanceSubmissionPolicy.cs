namespace SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

public sealed record AttendanceSubmissionContext(
    Guid RegistrationId,
    string RegistrationStatus,
    string SessionStatus,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AlreadySubmitted);

public static class ClassAttendanceSubmissionPolicy
{
    public static bool IsWithinWindow(DateTime startsAt, DateTime endsAt, DateTime nowUtc) =>
        startsAt <= nowUtc && nowUtc <= endsAt.AddHours(24);

    public static void EnsureAllowed(AttendanceSubmissionContext context, DateTime nowUtc)
    {
        if (context.AlreadySubmitted)
            throw new SwimClassStateConflictException("Bạn đã nộp ảnh điểm danh cho buổi học này.");
        if (context.RegistrationStatus != "Confirmed")
            throw new SwimClassStateConflictException("Chỉ đăng ký Confirmed được nộp ảnh điểm danh.");
        if (context.SessionStatus != "Scheduled")
            throw new SwimClassStateConflictException("Buổi học đã hủy, không thể nộp ảnh điểm danh.");
        if (!IsWithinWindow(context.StartsAt, context.EndsAt, nowUtc))
            throw new SwimClassStateConflictException("Buổi học chưa bắt đầu hoặc đã quá hạn nộp ảnh điểm danh.");
    }
}
