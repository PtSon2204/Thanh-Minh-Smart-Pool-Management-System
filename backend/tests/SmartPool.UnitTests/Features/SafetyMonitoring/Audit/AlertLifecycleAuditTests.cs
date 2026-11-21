using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.SafetyMonitoring.Audit;

public sealed class AlertLifecycleAuditTests
{
    private readonly Mock<IMonitoringRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IMonitoringWriteSession> session = new(MockBehavior.Strict);
    private readonly Mock<INotificationService> notifications = new(MockBehavior.Strict);
    private readonly FrozenClock clock = new();
    private Warning? current;
    private readonly List<Warning> added = [];
    private readonly List<Incident> incidents = [];
    private bool committed;
    private readonly List<string> events = [];
    private readonly List<string> prefixes = [];
    private readonly Guid camera = Guid.NewGuid();
    private readonly Guid user = Guid.NewGuid();
    private readonly CancellationTokenSource cancellation = new();
    private SafetyMonitoringService Service => new(repository.Object, notifications.Object, Options.Create(new RiskOptions()), clock,
        NullLogger<SafetyMonitoringService>.Instance);
    private RecordRiskCommand Command(string severity = "WARNING", string? image = "https://example.test/image.jpg") =>
        new(camera, new("track", "episode", severity, .9, 1, [.1, .1, .9, .9]), .9, clock.GetUtcNow().UtcDateTime, image);
    private Warning Existing(string status = "PENDING", string severity = "WARNING", double elapsed = 0) => new() {
        Id = Guid.NewGuid(), CamId = camera, Status = status, SeverityLevel = severity,
        VerifiedAt = clock.GetUtcNow().UtcDateTime.AddSeconds(-elapsed), ImageUrl = "https://example.test/old.jpg", TrackId = "track" };
    private AlertDto Dto() => new(current!.Id, camera, "Camera", "Pool", "track", current.RiskEpisode, current.SeverityLevel,
        current.Status, current.ImageUrl, current.SnapshotStatus, current.IncidentId, current.VerifiedBy, current.VerifiedAt, current.CreatedAt);

    public AlertLifecycleAuditTests()
    {
        session.SetupGet(x => x.Current).Returns(() => current);
        session.Setup(x => x.AddWarning(It.IsAny<Warning>())).Callback<Warning>(w => { added.Add(w); current = w; });
        session.Setup(x => x.AddIncident(It.IsAny<Incident>())).Callback<Incident>(incidents.Add);
        session.Setup(x => x.GetCameraAsync(camera, cancellation.Token)).ReturnsAsync(new Equipment { Id = camera, Name = "Camera", Location = "Pool" });
        repository.Setup(x => x.WithEpisodeAsync(It.IsAny<string>(), It.IsAny<Func<IMonitoringWriteSession, CancellationToken, Task<(Guid Id, string? Event)>>>(), cancellation.Token))
            .Returns(async (string prefix, Func<IMonitoringWriteSession, CancellationToken, Task<(Guid Id, string? Event)>> apply, CancellationToken ct) => {
                prefixes.Add(prefix); var result = await apply(session.Object, ct); committed = true; return result;
            });
        repository.Setup(x => x.WithAlertAsync(It.IsAny<Guid>(), It.IsAny<Func<IMonitoringWriteSession, CancellationToken, Task<bool>>>(), cancellation.Token))
            .Returns(async (Guid id, Func<IMonitoringWriteSession, CancellationToken, Task<bool>> apply, CancellationToken ct) => {
                var result = await apply(session.Object, ct); committed = true; return result;
            });
        repository.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), cancellation.Token)).ReturnsAsync(() => Dto());
        repository.Setup(x => x.FindEpisodeAsync(It.IsAny<string>(), cancellation.Token)).ReturnsAsync(() => current);
        notifications.Setup(x => x.SendToGroupAsync("safety-operators", It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, object, CancellationToken>((group, name, dto, ct) => { Assert.True(committed); events.Add(name); })
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("L01", "PENDING", "WARNING", "WARNING", 0, "ignore")]
    [InlineData("L02", "PENDING", "WARNING", "CRITICAL", 0, "escalate")]
    [InlineData("L03", "PENDING", "CRITICAL", "WARNING", 0, "ignore")]
    [InlineData("L04", "PENDING", "CRITICAL", "CRITICAL", 0, "ignore")]
    [InlineData("L05", "CANCELLED", "WARNING", "WARNING", 14.999999, "ignore")]
    [InlineData("L06", "CANCELLED", "WARNING", "WARNING", 15, "create")]
    [InlineData("L07", "CANCELLED", "WARNING", "WARNING", 15.000001, "create")]
    [InlineData("L08", "CANCELLED", "WARNING", "CRITICAL", 0, "create")]
    [InlineData("L09", "ACKNOWLEDGED", "WARNING", "WARNING", 100, "ignore")]
    [InlineData("L10", "ACKNOWLEDGED", "WARNING", "CRITICAL", 0, "create")]
    [InlineData("L11", "ACKNOWLEDGED", "CRITICAL", "CRITICAL", 100, "ignore")]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_ExistingEpisode_AppliesLifecycleMatrix(string utcId, string status, string severity, string incoming, double elapsed, string action)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        current = Existing(status, severity, elapsed);
        var old = current; var command = Command(incoming);
        var result = await Service.RecordAsync(command, cancellation.Token);
        Assert.Equal(action == "create" ? 1 : 0, added.Count);
        Assert.Equal(action == "ignore" ? 0 : 1, events.Count);
        Assert.Equal(current!.Id, result.Alert.Id);
        if (action == "create") { Assert.NotEqual(old.Id, current.Id); Assert.Equal("PENDING", current.Status); }
        else Assert.Same(old, current);
        if (action == "escalate") { Assert.Equal("CRITICAL", current.SeverityLevel); Assert.Equal(command.ImageUrl, current.ImageUrl); }
        if (action == "ignore") { Assert.Equal(status, current.Status); Assert.Equal(severity, current.SeverityLevel); }
        if (action != "ignore") { Assert.Equal("COMPLETED", current.SnapshotStatus); Assert.Equal(clock.GetUtcNow().UtcDateTime, current.UpdatedAt); }
        Assert.Empty(incidents);
    }

    [Theory]
    [InlineData("L12", -.1, 0)]
    [InlineData("L13", .5, .5)]
    [InlineData("L14", 1.1, 1)]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_NewEpisode_PersistsImageAndClampedConfidence(string utcId, double confidence, double expected)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var id = Guid.NewGuid(); var command = Command() with { Confidence = confidence, AlertId = id };
        var result = await Service.RecordAsync(command, cancellation.Token);
        var saved = Assert.Single(added);
        Assert.Equal(id, saved.Id); Assert.Equal(id, result.Alert.Id);
        Assert.Equal((decimal)expected, saved.Confidence); Assert.Equal(command.Decision.Box, saved.Box);
        Assert.Equal(command.ObservedAt, saved.WarningTime); Assert.Equal(command.ImageUrl, saved.ImageUrl);
        Assert.StartsWith(Assert.Single(prefixes), saved.RiskEpisode!);
        Assert.Equal(new[] { "alert_created" }, events);
    }

    [Theory]
    [InlineData("L15", "http://example.test/x")]
    [InlineData("L16", "https://user:pass@example.test/x")]
    [InlineData("L17", "/relative.jpg")]
    [InlineData("L18", "")]
    [InlineData("L19", null)]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_UntrustedImage_RejectsBeforeRepository(string utcId, string? image)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        await Assert.ThrowsAsync<ArgumentException>(() => Service.RecordAsync(Command(image: image), cancellation.Token));
        repository.VerifyNoOtherCalls(); notifications.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("L20", "NORMAL")]
    [InlineData("L21", "EMERGENCY")]
    [InlineData("L22", "warning")]
    [Trait("Function", "SafetyMonitoringService.PrepareSnapshotAsync")]
    public async Task PrepareSnapshotAsync_InvalidAiSeverity_RejectsBeforeRead(string utcId, string severity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        await Assert.ThrowsAsync<ArgumentException>(() => Service.PrepareSnapshotAsync(Command(severity), cancellation.Token));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("L23", "none", "WARNING", true)]
    [InlineData("L24", "PENDING", "WARNING", false)]
    [InlineData("L25", "PENDING", "CRITICAL", true)]
    [Trait("Function", "SafetyMonitoringService.PrepareSnapshotAsync")]
    public async Task PrepareSnapshotAsync_CurrentEpisode_ReturnsTargetWithoutMutation(string utcId, string status, string severity, bool target)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        current = status == "none" ? null : Existing();
        var before = current; var result = await Service.PrepareSnapshotAsync(Command(severity), cancellation.Token);
        Assert.Equal(target, result.HasValue); Assert.Same(before, current);
        if (before != null && target) Assert.Equal(before.Id, result);
        Assert.Empty(added); Assert.Empty(events); Assert.False(committed);
        repository.Verify(x => x.FindEpisodeAsync(It.IsAny<string>(), cancellation.Token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("L26", "ACKNOWLEDGED", "WARNING", "WARNING")]
    [InlineData("L27", "ACKNOWLEDGED", "CRITICAL", "EMERGENCY")]
    [InlineData("L28", "ACKNOWLEDGED", "EMERGENCY", "EMERGENCY")]
    [InlineData("L29", "CANCELLED", "CRITICAL", null)]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_PendingAlert_UpdatesAuditAndCreatesOnlyApplicableIncident(string utcId, string action, string severity, string? incidentSeverity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        current = Existing(severity: severity);
        var result = await Service.HandleAsync(current.Id, action, user, true, cancellation.Token, severity);
        Assert.Equal(action, result.Status); Assert.Equal(user, current.VerifiedBy);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, current.VerifiedAt);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, current.UpdatedAt);
        if (incidentSeverity == null) { Assert.Empty(incidents); Assert.Equal(new[] { "alert_updated" }, events); }
        else { var incident = Assert.Single(incidents); Assert.Equal(incidentSeverity, incident.SeverityLevel);
            Assert.Equal("Resolved", incident.Status); Assert.Equal(current.Id, incident.WarningId); Assert.Equal(current.ImageUrl, incident.ImageIncidents);
            Assert.Equal(incident.Id, current.IncidentId); Assert.Equal(new[] { "alert_updated", "incident_created" }, events); }
    }

    [Theory]
    [InlineData("L30", "ACKNOWLEDGED")]
    [InlineData("L31", "CANCELLED")]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_AlreadyHandled_RejectsWithoutMutationOrEvent(string utcId, string status)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        current = Existing(status);
        await Assert.ThrowsAsync<MonitoringConflictException>(() => Service.HandleAsync(current.Id, "ACKNOWLEDGED", user, true, cancellation.Token));
        Assert.Equal(status, current.Status); Assert.False(committed); Assert.Empty(incidents); Assert.Empty(events);
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_AlertMissing_ThrowsKeyNotFoundWithoutEvent()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.HandleAsync(Guid.NewGuid(), "CANCELLED", user, true, cancellation.Token));
        Assert.False(committed); Assert.Empty(events);
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_ExistingIncident_DoesNotCreateAnother()
    {
        current = Existing(); current.IncidentId = Guid.NewGuid(); var id = current.IncidentId;
        await Service.HandleAsync(current.Id, "ACKNOWLEDGED", user, true, cancellation.Token);
        Assert.Equal(id, current.IncidentId); Assert.Empty(incidents); Assert.Equal(new[] { "alert_updated" }, events);
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_AcknowledgementWithoutImage_RejectsBeforeMutation()
    {
        current = Existing(); current.ImageUrl = " ";
        await Assert.ThrowsAsync<MonitoringConflictException>(() => Service.HandleAsync(current.Id, "ACKNOWLEDGED", user, true, cancellation.Token));
        Assert.Equal("PENDING", current.Status); Assert.False(committed); Assert.Empty(events);
    }

    [Theory]
    [InlineData("L32", "DELETE", null)]
    [InlineData("L33", "acknowledged", null)]
    [InlineData("L34", "CANCELLED", "NORMAL")]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_InvalidActionOrSeverity_RejectsBeforeRepository(string utcId, string action, string? severity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        await Assert.ThrowsAsync<ArgumentException>(() => Service.HandleAsync(Guid.NewGuid(), action, user, true, cancellation.Token, severity));
        repository.VerifyNoOtherCalls(); notifications.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.HandleAsync")]
    public async Task HandleAsync_RepositoryCancelled_PropagatesTokenWithoutPublishing()
    {
        cancellation.Cancel();
        repository.Setup(x => x.WithAlertAsync(It.IsAny<Guid>(), It.IsAny<Func<IMonitoringWriteSession, CancellationToken, Task<bool>>>(), cancellation.Token))
            .Returns(Task.FromCanceled<bool>(cancellation.Token));
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.HandleAsync(Guid.NewGuid(), "CANCELLED", user, true, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken); Assert.Empty(events);
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_RepositoryFails_DoesNotReadOrPublish()
    {
        repository.Setup(x => x.WithEpisodeAsync(It.IsAny<string>(), It.IsAny<Func<IMonitoringWriteSession, CancellationToken, Task<(Guid Id, string? Event)>>>(), cancellation.Token))
            .ThrowsAsync(new IOException("write failed"));
        await Assert.ThrowsAsync<IOException>(() => Service.RecordAsync(Command(), cancellation.Token));
        repository.Verify(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_NotificationFails_ReturnsCommittedAlert()
    {
        notifications.Setup(x => x.SendToGroupAsync("safety-operators", "alert_created", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("transport failed"));
        var result = await Service.RecordAsync(Command(), cancellation.Token);
        Assert.True(committed); Assert.Equal(Assert.Single(added).Id, result.Alert.Id);
        notifications.Verify(x => x.SendToGroupAsync("safety-operators", "alert_created", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Function", "SafetyMonitoringService.RecordAsync")]
    public async Task RecordAsync_DifferentCameras_UsesSeparateEpisodeLockKeys()
    {
        var command = Command(); await Service.RecordAsync(command, cancellation.Token);
        current = null; await Service.RecordAsync(command with { CameraId = Guid.NewGuid() }, cancellation.Token);
        Assert.Equal(2, prefixes.Distinct().Count()); Assert.Equal(2, added.Count);
    }

    [Theory]
    [InlineData("L35", false)]
    [InlineData("L36", true)]
    [Trait("Function", "SafetyMonitoringService.CompleteSnapshotAsync")]
    public async Task CompleteSnapshotAsync_ExistingAlert_UpdatesPhotoAndLinkedIncident(string utcId, bool linked)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        current = Existing(); current.IncidentId = linked ? Guid.NewGuid() : null;
        const string url = "https://example.test/new.jpg";
        if (linked) session.Setup(x => x.UpdateIncidentImageAsync(current.IncidentId!.Value, url, cancellation.Token)).Returns(Task.CompletedTask);
        await Service.CompleteSnapshotAsync(current.Id, url, cancellation.Token);
        Assert.Equal(url, current.ImageUrl); Assert.Equal("COMPLETED", current.SnapshotStatus);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, current.UpdatedAt); Assert.Equal(new[] { "alert_updated" }, events);
        session.Verify(x => x.UpdateIncidentImageAsync(It.IsAny<Guid>(), url, cancellation.Token), linked ? Times.Once : Times.Never);
    }

    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    }
}
