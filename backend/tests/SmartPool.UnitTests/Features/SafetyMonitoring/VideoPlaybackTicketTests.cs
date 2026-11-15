using Microsoft.AspNetCore.DataProtection;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class VideoPlaybackTicketTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Ticket_IsBoundToOneJob_AndRejectsTamperingAndExpiry()
    {
        var clock = new Clock();
        var tickets = new VideoPlaybackTickets(new EphemeralDataProtectionProvider(), clock);
        var job = Guid.NewGuid();
        var issued = tickets.Create(job);
        Assert.True(tickets.Validate(job, issued.Value));
        Assert.False(tickets.Validate(Guid.NewGuid(), issued.Value));
        var middle = issued.Value.Length / 2;
        var tampered = issued.Value[..middle] + (issued.Value[middle] == 'a' ? "b" : "a") + issued.Value[(middle + 1)..];
        Assert.False(tickets.Validate(job, tampered));
        Assert.False(tickets.Validate(job, "not-a-ticket"));
        Assert.False(tickets.Validate(job, ""));
        clock.Now = issued.ExpiresAt;
        Assert.False(tickets.Validate(job, issued.Value));
    }
}
