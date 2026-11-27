namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CompleteSwimClass;

public static class ClassCompletionPolicy
{
    public static void EnsureCanComplete(string status, IReadOnlyCollection<DateTime> scheduledEnds,
        bool hasPendingRegistrations, bool hasMissingAttendances, DateTime nowUtc)
    {
        if (status != "Active")
            throw new SwimClassStateConflictException("Chỉ có thể kết thúc lớp đang hoạt động.");
        if (scheduledEnds.Count == 0 || scheduledEnds.Any(end => end >= nowUtc))
            throw new SwimClassStateConflictException("Chỉ kết thúc lớp sau khi tất cả buổi học đã qua.");
        if (hasPendingRegistrations)
            throw new SwimClassStateConflictException("Hãy xử lý các yêu cầu tham gia còn chờ trước khi kết thúc lớp.");
        if (hasMissingAttendances)
            throw new SwimClassStateConflictException("Hãy hoàn tất điểm danh các buổi trước khi kết thúc lớp.");
    }
}
