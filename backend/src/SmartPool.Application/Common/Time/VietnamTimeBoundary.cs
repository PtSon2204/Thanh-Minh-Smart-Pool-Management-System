namespace SmartPool.Application.Common.Time;

public static class VietnamTimeBoundary
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateOnly GetDate(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTimeZone).DateTime);

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDayBounds(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), VietnamTimeZone));
    }

    public static DateTime GetNextUtcDayBoundary(DateTime utcInstant)
    {
        var utcDateTime = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
        var vietnamDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcDateTime, VietnamTimeZone));
        return GetUtcDayBounds(vietnamDate).EndUtc;
    }

    public static bool HasStarted(DateTime? issueDate, DateTime utcNow) =>
        issueDate is null || issueDate <= utcNow;

    public static bool HasNotExpired(DateTime? expiryDate, DateTime utcNow) =>
        expiryDate is null || expiryDate > utcNow;
}
