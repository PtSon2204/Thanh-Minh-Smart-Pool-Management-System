using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.SafetyMonitoring.Audit;

public sealed class MonitoringContractsAuditTests
{
    [Theory]
    [InlineData("Q01", 0, 20, null, false)]
    [InlineData("Q02", 1, 1, null, true)]
    [InlineData("Q03", 100000, 100, "PENDING", true)]
    [InlineData("Q04", 100001, 20, null, false)]
    [InlineData("Q05", 1, 0, null, false)]
    [InlineData("Q06", 1, 101, null, false)]
    [InlineData("Q07", 1, 20, "ACKNOWLEDGED", true)]
    [InlineData("Q08", 1, 20, "CANCELLED", true)]
    [InlineData("Q09", 1, 20, "pending", false)]
    [InlineData("Q10", 1, 20, "", false)]
    [Trait("Function", "GetAlertsHandler.Handle")]
    public async Task Handle_FilterLimits_ValidatesBeforeDelegatingIdentityAndToken(string utcId, int page, int size, string? status, bool valid)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var userId = Guid.NewGuid(); using var stop = new CancellationTokenSource();
        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(x => x.UserId).Returns(userId); user.Setup(x => x.IsInRole("ADMIN")).Returns(true);
        var service = new Mock<ISafetyMonitoringService>(MockBehavior.Strict);
        var query = new GetAlertsQuery(Page: page, PageSize: size, Status: status);
        var expected = new PagedResponse<AlertDto>();
        service.Setup(x => x.ListAsync(query, userId, true, stop.Token)).ReturnsAsync(expected);
        var handler = new GetAlertsHandler(service.Object, user.Object);
        if (valid) { Assert.Same(expected, await handler.Handle(query, stop.Token));
            service.Verify(x => x.ListAsync(query, userId, true, stop.Token), Times.Once); }
        else { await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, stop.Token)); user.VerifyNoOtherCalls(); }
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Q11", "list")]
    [InlineData("Q12", "get")]
    [InlineData("Q13", "handle")]
    [Trait("Function", "MonitoringHandlers.Identity")]
    public void Handle_MissingIdentity_ThrowsBeforeService(string utcId, string operation)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var user = new Mock<ICurrentUser>(); var service = new Mock<ISafetyMonitoringService>(MockBehavior.Strict);
        Assert.Throws<UnauthorizedAccessException>(() => {
            return operation switch {
                "list" => (object)new GetAlertsHandler(service.Object, user.Object).Handle(new(), default),
                "get" => new GetAlertHandler(service.Object, user.Object).Handle(new(Guid.NewGuid()), default),
                _ => new HandleAlertHandler(service.Object, user.Object).Handle(new(Guid.NewGuid(), "CANCELLED"), default)
            };
        });
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Q14", "CANCELLED", "NORMAL")]
    [InlineData("Q15", "cancelled", "CRITICAL")]
    [Trait("Function", "HandleAlertHandler.Handle")]
    public async Task Handle_InvalidCommand_ThrowsBeforeService(string utcId, string action, string severity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var service = new Mock<ISafetyMonitoringService>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new HandleAlertHandler(service.Object, Mock.Of<ICurrentUser>()).Handle(new(Guid.NewGuid(), action, severity), default));
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Q16", false)]
    [InlineData("Q17", true)]
    [Trait("Function", "HandleAlertHandler.Handle")]
    public async Task Handle_ValidCommand_ForwardsSeverityIdentityAndCancellation(string utcId, bool admin)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var userId = Guid.NewGuid(); using var stop = new CancellationTokenSource();
        var user = new Mock<ICurrentUser>(); user.SetupGet(x => x.UserId).Returns(userId); user.Setup(x => x.IsInRole("ADMIN")).Returns(admin);
        var command = new HandleAlertCommand(Guid.NewGuid(), "ACKNOWLEDGED", "CRITICAL");
        var service = new Mock<ISafetyMonitoringService>(MockBehavior.Strict);
        service.Setup(x => x.HandleAsync(command.Id, command.Action, userId, admin, stop.Token, command.ExpectedSeverity)).ReturnsAsync((AlertDto)null!);
        Assert.Null(await new HandleAlertHandler(service.Object, user.Object).Handle(command, stop.Token));
        service.Verify(x => x.HandleAsync(command.Id, command.Action, userId, admin, stop.Token, command.ExpectedSeverity), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("C01", "ack", null)]
    [InlineData("C02", "ack", "")]
    [InlineData("C03", "cancel", " ")]
    [Trait("Function", "AlertsController.Acknowledge/Cancel")]
    public async Task AcknowledgeOrCancel_MissingExpectedSeverity_ReturnsBadRequestWithoutSending(string utcId, string action, string? severity)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var sender = new Mock<ISender>(MockBehavior.Strict); var controller = new AlertsController(sender.Object);
        var request = severity == null ? null : new AcknowledgeAlertRequest(severity);
        var result = action == "ack" ? await controller.Acknowledge(Guid.NewGuid(), request, default) : await controller.Cancel(Guid.NewGuid(), request, default);
        Assert.IsType<BadRequestObjectResult>(result); sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("C04", "missing", 404)]
    [InlineData("C05", "conflict", 409)]
    [InlineData("C06", "argument", 400)]
    [Trait("Function", "AlertsController.Get")]
    public async Task Get_KnownApplicationException_MapsHttpStatus(string utcId, string error, int status)
    {
        _ = utcId; // Stable case ID appears in the xUnit/TRX display name.
        var id = Guid.NewGuid(); var sender = new Mock<ISender>(MockBehavior.Strict);
        Exception exception = error switch { "missing" => new KeyNotFoundException(), "conflict" => new MonitoringConflictException("changed"), _ => new ArgumentException("invalid") };
        sender.Setup(x => x.Send(It.Is<GetAlertQuery>(q => q.Id == id), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        var result = await new AlertsController(sender.Object).Get(id, default);
        Assert.Equal(status, result is ObjectResult obj ? obj.StatusCode : ((StatusCodeResult)result).StatusCode);
    }
}
