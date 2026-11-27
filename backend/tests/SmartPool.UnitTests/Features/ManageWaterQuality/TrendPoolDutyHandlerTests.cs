using Microsoft.Extensions.Logging;
using Moq;
using SmartPool.Application.Features.ManageWaterQuality.Commands.TrendPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class TrendPoolDutyHandlerTests
{
    private readonly Mock<IWaterQualityOperations> _operationsMock = new();
    private readonly Mock<INotificationOperations> _notificationOpsMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    [Fact(DisplayName = "UTC_WQ_TDUTY01 (N) - Admin phân công lịch trực xu hướng 3 ngày -> Gửi thông báo nhân viên và SignalR")]
    public async Task AssignTrendDuty_NotifiesAssignedEmployeeAndBroadcasts()
    {
        var scheduleId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var dutyDate = new DateOnly(2026, 11, 23);

        var expected = new PoolDutyScheduleDto
        {
            Id = scheduleId,
            DutyDate = dutyDate,
            AssignedEmployeeId = employeeId,
            AssignedEmployeeName = "Nguyễn Văn A",
            Status = "Scheduled",
            IsTrendDuty = true,
            SuggestedChemicals = ["Soda Ash (Na2CO3)", "Trợ lắng PAC / Phèn nhôm"]
        };

        _operationsMock
            .Setup(o => o.AssignTrendPoolDutyScheduleAsync(scheduleId, employeeId, null, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new AssignTrendPoolDutyHandler(
            _operationsMock.Object,
            _notificationOpsMock.Object,
            _notificationServiceMock.Object,
            TimeProvider.System,
            new Mock<ILogger<AssignTrendPoolDutyHandler>>().Object);

        var result = await handler.Handle(
            new AssignTrendPoolDutyCommand(scheduleId, employeeId, null) { ActorId = actorId },
            CancellationToken.None);

        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(employeeId, result.AssignedEmployeeId);
        _notificationOpsMock.Verify(n => n.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(list => list.Single().UserId == employeeId && list.Single().Type == "WATER_QUALITY_TREND_DUTY"),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToUserAsync(
            employeeId.ToString(), "ReceiveNotification", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "WaterQualityUpdated", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_WQ_TDUTY02 (N) - Lifeguard hoàn thành lịch trực xu hướng 3 ngày -> Broadcast WaterQualityUpdated")]
    public async Task CompleteTrendDuty_BroadcastsWaterQualityUpdated()
    {
        var scheduleId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var expected = new PoolDutyScheduleDto
        {
            Id = scheduleId,
            DutyDate = new DateOnly(2026, 11, 23),
            AssignedEmployeeId = actorId,
            Status = "Completed",
            WaterQualityLogId = logId,
            IsTrendDuty = true
        };

        _operationsMock
            .Setup(o => o.CompleteTrendPoolDutyScheduleAsync(scheduleId, actorId, logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new CompleteTrendPoolDutyHandler(
            _operationsMock.Object,
            _notificationServiceMock.Object,
            new Mock<ILogger<CompleteTrendPoolDutyHandler>>().Object);

        var result = await handler.Handle(
            new CompleteTrendPoolDutyCommand(scheduleId, logId) { ActorId = actorId },
            CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "WaterQualityUpdated", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
