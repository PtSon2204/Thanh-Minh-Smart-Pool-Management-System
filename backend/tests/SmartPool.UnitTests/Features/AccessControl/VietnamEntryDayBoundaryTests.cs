using SmartPool.Application.Common.Time;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class VietnamEntryDayBoundaryTests
{
    [Theory]
    [InlineData("2026-10-04T16:59:59Z", "2026-10-04")]
    [InlineData("2026-10-04T17:00:00Z", "2026-10-05")]
    public void GivenUtcInstant_WhenDerivingVietnamDate_ThenUsesVietnamMidnight(string utcValue, string expectedDate)
    {
        var utcNow = DateTime.Parse(utcValue, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        var vietnamDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone));

        Assert.Equal(DateOnly.Parse(expectedDate), vietnamDate);
    }

    [Fact]
    public void GivenVietnamDate_WhenGettingUtcBounds_ThenReturnsHalfOpenDayInterval()
    {
        var (startUtc, endUtc) = VietnamTimeBoundary.GetUtcDayBounds(new DateOnly(2026, 10, 5));

        Assert.Equal(new DateTime(2026, 10, 4, 17, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 10, 5, 17, 0, 0, DateTimeKind.Utc), endUtc);
    }
}
