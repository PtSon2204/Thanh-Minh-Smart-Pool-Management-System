using Moq;
using SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;
using SmartPool.Application.Features.ManageSafetyAlerts.Commands.ConfirmSafetyIncident;
using SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class SafetyIncidentFlowTests
{
    private readonly Mock<ISafetyAlertOperations> operations = new();
    private readonly Mock<INotificationService> notifications = new();

    private GetSafetyIncidentsHandler History(params Incident[] rows)
    {
        operations.Setup(x => x.GetAllIncidentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows.ToList());
        operations.Setup(x => x.GetAllUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        operations.Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserProfile>());
        return new(operations.Object);
    }

    [Fact]
    public async Task EmptyHistory_ReturnsEmptyRealData()
    {
        var result = await History().Handle(new(), default);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalIncidents);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CameraWithoutSnapshot_StaysPendingAndKeepsLocation()
    {
        var row = new Incident { Id = Guid.NewGuid(), Title = "Camera A", WarningId = Guid.NewGuid(),
            Location = "Khu vực A", Status = "Pending", SeverityLevel = "EMERGENCY" };
        var result = await History(row).Handle(new(), default);
        var dto = Assert.Single(result.Items);
        Assert.Equal("Pending", dto.Status);
        Assert.Equal("Khu vực A", dto.Zone);
        Assert.Equal("Camera AI giám sát", dto.SourceType);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.CameraCount);
    }

    [Fact]
    public async Task SourceFilter_AppliesBeforePaginationAndStatistics_ManualPhotoDoesNotBecomeAi()
    {
        var rows = Enumerable.Range(0, 3).Select(i => new Incident { Id = Guid.NewGuid(), Title = "Manual",
            Status = "Pending", ImageIncidents = "photo.jpg", SeverityLevel = "WARNING" }).ToList();
        rows.Add(new() { Id = Guid.NewGuid(), Title = "AI", CameraId = Guid.NewGuid(), Status = "Resolved", SeverityLevel = "EMERGENCY" });
        var result = await History(rows.ToArray()).Handle(new() { SourceType = "camera", PageSize = 1 }, default);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalIncidents);
        Assert.Equal(1, result.CameraCount);
        Assert.Equal(0, result.LifeguardCount);
    }

    [Fact]
    public async Task Activation_PersistsPendingBeforeBroadcast_AndEchoesCorrelation()
    {
        Incident? saved = null;
        var committed = false;
        operations.Setup(x => x.AddIncidentAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()))
            .Callback<Incident, CancellationToken>((row, _) =>
            {
                saved = row;
                committed = true;
            }).Returns(Task.CompletedTask);
        notifications.Setup(x => x.SendToAllAsync("safety_alert", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(committed)).Returns(Task.CompletedTask);
        var correlation = Guid.NewGuid();
        var result = await new ActivateSafetyAlertHandler(notifications.Object, operations.Object)
            .Handle(new() { Zone = "Bể trẻ em", ClientRequestId = correlation }, default);
        Assert.Equal(correlation, result.ClientRequestId);
        Assert.NotNull(saved);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal("Bể trẻ em", saved.Location);
        Assert.Equal(saved.Id, result.AlertId);
    }

    [Fact]
    public async Task FailedSave_DoesNotReportSuccessfulActivationOrBroadcast()
    {
        operations.Setup(x => x.AddIncidentAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("DB failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ActivateSafetyAlertHandler(notifications.Object, operations.Object).Handle(new() { Zone = "Bể lớn" }, default));
        notifications.Verify(x => x.SendToAllAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMissingIncident_DoesNotReportSuccessOrBroadcast()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ConfirmSafetyIncidentHandler(operations.Object, notifications.Object).Handle(new() { IncidentId = Guid.NewGuid() }, default));
        operations.Verify(x => x.UpdateIncidentAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(x => x.SendToAllAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmInvalidStatus_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ConfirmSafetyIncidentHandler(operations.Object, notifications.Object).Handle(new() { Status = "arbitrary" }, default));
    }
}
