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

    [Fact]
    public async Task History_DelegatesValidatedFiltersAndPagingToTheDatabaseReader()
    {
        var reader = new Mock<ISafetyIncidentReader>();
        var query = new GetSafetyIncidentsQuery { SourceType = "camera", PageSize = 1, Status = "Resolved" };
        var expected = new GetSafetyIncidentsResult { TotalIncidents = 3, CameraCount = 3, TotalCount = 3 };
        reader.Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        Assert.Same(expected, await new GetSafetyIncidentsHandler(reader.Object).Handle(query, default));
        reader.Verify(x => x.SearchAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetSafetyIncidentsHandler(reader.Object).Handle(new() { PageSize = 101 }, default));
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public void CameraWithoutSnapshot_StaysPendingAndKeepsLocation()
    {
        var row = new Incident { Id = Guid.NewGuid(), Title = "Camera A", WarningId = Guid.NewGuid(),
            Location = "Khu vực A", Status = "Pending", SeverityLevel = "EMERGENCY" };
        var dto = SafetyIncidentMapper.ToDto(row, null, null);
        Assert.Equal("Pending", dto.Status);
        Assert.Equal("Khu vực A", dto.Zone);
        Assert.Equal("Camera AI giám sát", dto.SourceType);
    }

    [Fact]
    public void ManualPhotoDoesNotBecomeCameraAi_AndLegacyStatusIsNormalized()
    {
        var dto = SafetyIncidentMapper.ToDto(new Incident { Id = Guid.NewGuid(), Title = "Bể trẻ em",
            Status = "completed", ImageIncidents = "photo.jpg", SeverityLevel = "WARNING" }, null, null);
        Assert.Equal("Cứu hộ viên phát hiện thực tế", dto.SourceType);
        Assert.Equal("Resolved", dto.Status);
        Assert.Equal("Bể trẻ em", dto.Zone);
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

    [Theory]
    [InlineData("Pending")]
    [InlineData("In Progress")]
    [InlineData("Resolved")]
    public async Task ConfirmValidIncident_PersistsStatusAndNoteBeforeNotification(string status)
    {
        using var cancellation = new CancellationTokenSource();
        var incident = new Incident { Id = Guid.NewGuid(), Status = "Pending", Description = "Original report" };
        var saved = false;
        operations.Setup(x => x.GetIncidentByIdAsync(incident.Id, cancellation.Token)).ReturnsAsync(incident);
        operations.Setup(x => x.UpdateIncidentAsync(incident, cancellation.Token))
            .Callback(() => saved = true).Returns(Task.CompletedTask);
        notifications.Setup(x => x.SendToAllAsync("safety_incident_confirmed", It.IsAny<object>(), cancellation.Token))
            .Callback(() => Assert.True(saved)).Returns(Task.CompletedTask);
        var result = await new ConfirmSafetyIncidentHandler(operations.Object, notifications.Object).Handle(new() {
            IncidentId = incident.Id, Status = status, ResolutionNote = "Đã kiểm tra tại hiện trường", ConfirmedByName = "Cứu hộ A"
        }, cancellation.Token);
        Assert.Equal(status, incident.Status);
        Assert.Equal(status, result.Status);
        Assert.Equal(incident.Id, result.IncidentId);
        Assert.Equal(result.ConfirmedAt, incident.UpdatedAt);
        Assert.StartsWith("Original report\n", incident.Description);
        Assert.Contains("Cứu hộ A", incident.Description);
        Assert.Contains("Đã kiểm tra tại hiện trường", incident.Description);
        notifications.Verify(x => x.SendToAllAsync("safety_incident_confirmed", It.IsAny<object>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ConfirmSaveFailure_DoesNotPublishSuccessfulResolution()
    {
        var incident = new Incident { Id = Guid.NewGuid(), Status = "Pending" };
        operations.Setup(x => x.GetIncidentByIdAsync(incident.Id, default)).ReturnsAsync(incident);
        operations.Setup(x => x.UpdateIncidentAsync(incident, default)).ThrowsAsync(new IOException("write failed"));
        await Assert.ThrowsAsync<IOException>(() => new ConfirmSafetyIncidentHandler(operations.Object, notifications.Object)
            .Handle(new() { IncidentId = incident.Id, Status = "Resolved" }, default));
        notifications.VerifyNoOtherCalls();
    }
}
