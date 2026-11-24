using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class AlertFilterValidationTests
{
    [Fact]
    public async Task InvalidFilterBounds_RejectBeforeReadingAlerts()
    {
        var service = new Mock<ISafetyMonitoringService>(MockBehavior.Strict);
        var handler = new GetAlertsHandler(service.Object, Mock.Of<ICurrentUser>());
        var now = DateTimeOffset.UtcNow;
        foreach (var query in new[] {
            new GetAlertsQuery(FromTime: now, ToTime: now.AddSeconds(-1)),
            new GetAlertsQuery(Severity: "NORMAL"),
            new GetAlertsQuery(Area: new string('a', 201)) })
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, default));
        service.VerifyNoOtherCalls();
    }
}
