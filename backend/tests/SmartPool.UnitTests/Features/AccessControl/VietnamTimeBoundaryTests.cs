using SmartPool.Application.Common.Time;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class VietnamTimeBoundaryTests
{
    [Theory]
    [InlineData("2026-10-05T16:59:59+00:00", "2026-10-05")]
    [InlineData("2026-10-05T17:00:00+00:00", "2026-10-06")]
    [InlineData("2026-10-04T17:00:00+00:00", "2026-10-05")]
    public void GivenUtcInstant_WhenGettingVietnamDate_ThenDateUsesVietnamMidnightBoundary(
        string utcInstant,
        string expectedDate)
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse(utcInstant));

        var actualDate = VietnamTimeBoundary.GetDate(timeProvider);

        Assert.Equal(DateOnly.Parse(expectedDate), actualDate);
    }

    [Fact]
    public void GivenVietnamDate_WhenGettingUtcBoundaries_ThenBoundariesAreExclusiveMidnights()
    {
        var date = new DateOnly(2026, 10, 5);

        var (start, end) = VietnamTimeBoundary.GetUtcDayBounds(date);

        Assert.Equal(new DateTime(2026, 10, 4, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 5, 17, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void GivenAnIssueDateAfterTheFixedInstant_WhenCheckingStart_ThenTicketHasNotStarted()
    {
        var now = new DateTime(2026, 10, 5, 16, 0, 0, DateTimeKind.Utc);

        Assert.False(VietnamTimeBoundary.HasStarted(now.AddTicks(1), now));
        Assert.True(VietnamTimeBoundary.HasStarted(now, now));
    }

    [Fact]
    public void GivenAnExpiryAtOrBeforeTheFixedInstant_WhenCheckingExpiry_ThenTicketIsExpired()
    {
        var now = new DateTime(2026, 10, 5, 16, 0, 0, DateTimeKind.Utc);

        Assert.False(VietnamTimeBoundary.HasNotExpired(now, now));
        Assert.False(VietnamTimeBoundary.HasNotExpired(now.AddTicks(-1), now));
        Assert.True(VietnamTimeBoundary.HasNotExpired(now.AddTicks(1), now));
        Assert.True(VietnamTimeBoundary.HasNotExpired(null, now));
        Assert.True(VietnamTimeBoundary.HasStarted(null, now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
