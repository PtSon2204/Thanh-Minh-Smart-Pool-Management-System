using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class SafetyMonitoringServiceTests
{
    private readonly Mock<IMonitoringRepository> store = new();
    private readonly Mock<IMonitoringWriteSession> session = new();
    private readonly Mock<INotificationService> notifications = new();
    private readonly List<Incident> incidents = [];
    private readonly Warning warning = new() { Id = Guid.NewGuid(), CamId = Guid.NewGuid(), SourceType = "LIVE",
        Status = "PENDING", SeverityLevel = "CRITICAL", ImageUrl = "https://example.test/photo.jpg", WarningTime = DateTime.UtcNow };
    private bool committed;
    private SafetyMonitoringService Service => new(store.Object, notifications.Object, Options.Create(new RiskOptions()),
        TimeProvider.System, NullLogger<SafetyMonitoringService>.Instance);

    public SafetyMonitoringServiceTests()
    {
        session.SetupGet(x => x.Current).Returns(warning);
        session.Setup(x => x.GetCameraAsync(warning.CamId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Equipment { Id = warning.CamId!.Value, Name = "Camera A", Location = "Bể lớn" });
        session.Setup(x => x.AddIncident(It.IsAny<Incident>())).Callback<Incident>(incidents.Add);
        store.Setup(x => x.WithAlertAsync(warning.Id, It.IsAny<Func<IMonitoringWriteSession, CancellationToken, Task<(bool, bool)>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, Func<IMonitoringWriteSession, CancellationToken, Task<(bool, bool)>> apply, CancellationToken ct) => {
                var result = await apply(session.Object, ct); committed = true; return result;
            });
        store.Setup(x => x.GetAsync(warning.Id, It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AlertDto(warning.Id, warning.SourceType, warning.CamId, null, "Camera A", "Bể lớn", "1", "episode",
                warning.SeverityLevel, warning.Status, null, warning.ImageUrl, "COMPLETED", warning.IncidentId,
                warning.VerifiedBy, warning.VerifiedAt, DateTime.UtcNow));
        notifications.Setup(x => x.SendToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(committed, "Never publish uncommitted changes")).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CriticalLiveAcknowledgement_CreatesOneIncident_AndPublishesOnlyAfterCommit()
    {
        var user = Guid.NewGuid();
        var dto = await Service.HandleAsync(warning.Id, "ACKNOWLEDGED", user, true, default, "CRITICAL");
        var incident = Assert.Single(incidents);
        Assert.Equal(incident.Id, dto.IncidentId);
        Assert.Equal(warning.Id, incident.WarningId);
        Assert.Equal(warning.ImageUrl, incident.ImageIncidents);
        Assert.Equal(user, incident.ReportedBy);
        await Assert.ThrowsAsync<MonitoringConflictException>(() => Service.HandleAsync(warning.Id, "ACKNOWLEDGED", user, true, default));
        Assert.Single(incidents);
        notifications.Verify(x => x.SendToGroupAsync("safety-operators", "incident_created", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("WARNING", "LIVE")]
    [InlineData("CRITICAL", "UPLOAD")]
    public async Task AttentionAndUploadedVideoAcknowledgement_DoNotCreateIncident(string severity, string source)
    {
        warning.SeverityLevel = severity; warning.SourceType = source;
        await Service.HandleAsync(warning.Id, "ACKNOWLEDGED", Guid.NewGuid(), true, default, severity);
        Assert.Empty(incidents);
        notifications.Verify(x => x.SendToGroupAsync(It.IsAny<string>(), "incident_created", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StaleSeverity_DoesNotMutateOrCommitOrNotify()
    {
        await Assert.ThrowsAsync<MonitoringConflictException>(() => Service.HandleAsync(warning.Id, "ACKNOWLEDGED", Guid.NewGuid(), true, default, "WARNING"));
        Assert.Equal("PENDING", warning.Status);
        Assert.False(committed);
        Assert.Empty(incidents);
        notifications.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.test/photo.jpg")]
    public async Task MissingOrUntrustedImage_CannotStartAnAlertWrite(string? image)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.RecordAsync(new("LIVE", Guid.NewGuid(),
            new("1", "episode", "WARNING", .9, 1, [.1, .1, .3, .4]), .9, DateTime.UtcNow, image), default));
        store.VerifyNoOtherCalls();
        notifications.VerifyNoOtherCalls();
    }
}
