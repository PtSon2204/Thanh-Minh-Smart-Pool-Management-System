namespace SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;

public static class ManualAttendanceCorrectionPolicy
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static bool HasVietnamOffset(DateTimeOffset? value) => value?.Offset == TimeSpan.FromHours(7);

    public static bool IsNotFuture(DateTimeOffset value, DateTimeOffset now) => value.UtcDateTime <= now.UtcDateTime;

    public static bool IsWithinLookback(DateOnly workDate, DateOnly today, int lookbackDays)
    {
        var daysAgo = today.DayNumber - workDate.DayNumber;
        return daysAgo >= 0 && daysAgo <= lookbackDays;
    }

    public static DateOnly VietnamDate(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, VietnamTimeZone).DateTime);
}
