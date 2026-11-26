using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Services;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class CreateWaterQualityLogHandlerTests
{
    private readonly Mock<IWaterQualityOperations> _operationsMock = new();
    private readonly IWaterQualityEvaluator _evaluator = new WaterQualityEvaluator();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<INotificationOperations> _notificationOperationsMock = new();
    private readonly Mock<IUserOperations> _userOperationsMock = new();
    private readonly Mock<ILogger<CreateWaterQualityLogHandler>> _loggerMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly IValidator<CreateWaterQualityLogCommand> _validator;

    public CreateWaterQualityLogHandlerTests()
    {
        _validator = new CreateWaterQualityLogValidator(_timeProvider);
    }

    private CreateWaterQualityLogHandler CreateHandler()
    {
        return new CreateWaterQualityLogHandler(
            _operationsMock.Object,
            _evaluator,
            _validator,
            _currentUserMock.Object,
            _notificationServiceMock.Object,
            _notificationOperationsMock.Object,
            _userOperationsMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    [Fact(DisplayName = "UTC_CQ01 (N) - Nhập chỉ số chuẩn -> Lưu phiếu thành công với Status = WithinLimits")]
    public async Task UTC_CQ01_ValidInput_CreatesLogWithWithinLimitsStatus()
    {
        var inspectorId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(It.IsAny<WaterQualityLog>(),
                It.IsAny<CancellationToken>(), It.IsAny<OperationalTaskExecutionAuthorization?>()))
            .ReturnsAsync((WaterQualityLog log, CancellationToken cancellationToken, OperationalTaskExecutionAuthorization? authorization) => new WaterQualityLogDto
            {
                Id = log.Id,
                InspectionTime = log.InspectionTime,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL,
                Status = log.Status,
                Recommendation = log.Recommendation,
                InspectedBy = log.InspectedBy
            });

        var handler = CreateHandler();
        var command = new CreateWaterQualityLogCommand
        {
            InspectionTime = DateTime.UtcNow,
            PhValue = 7.4m,
            FreeChlorineMgL = 1.8m,
            IsWaterClear = true,
            Notes = "Đo ca sáng"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WaterQualityConstants.StatusWithinLimits, result.Status);
        Assert.Equal(inspectorId, result.InspectedBy);
        _operationsMock.Verify(o => o.CreateWaterQualityLogAsync(
            It.Is<WaterQualityLog>(l => l.Status == WaterQualityConstants.StatusWithinLimits && l.PhValue == 7.4m),
            It.IsAny<CancellationToken>(), It.IsAny<OperationalTaskExecutionAuthorization?>()), Times.Once);
    }

    [Fact]
    public async Task Lifeguard_task_execution_passes_server_role_and_expected_assignment_to_repository()
    {
        var lifeguardId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _currentUserMock.Setup(user => user.UserId).Returns(lifeguardId);
        _currentUserMock.Setup(user => user.IsInRole("LIFEGUARD")).Returns(true);
        _operationsMock.Setup(operations => operations.CreateWaterQualityLogAsync(
                It.Is<WaterQualityLog>(log => log.InspectedBy == lifeguardId && log.OperationalTaskId == taskId),
                It.IsAny<CancellationToken>(),
                It.Is<OperationalTaskExecutionAuthorization?>(authorization => authorization != null &&
                    !authorization.IsAdmin && authorization.IsLifeguard && !authorization.AdminExecutionConfirmed &&
                    authorization.ExpectedTaskRevision == 4 && authorization.ExpectedAssigneeId == lifeguardId)))
            .ReturnsAsync(new WaterQualityLogDto { Status = WaterQualityConstants.StatusWithinLimits });

        var result = await CreateHandler().Handle(new CreateWaterQualityLogCommand
        {
            OperationalTaskId = taskId,
            ExpectedTaskRevision = 4,
            ExpectedAssigneeId = lifeguardId,
            PhValue = 7.4m,
            FreeChlorineMgL = 1.8m,
            IsWaterClear = true
        }, CancellationToken.None);

        Assert.Equal(WaterQualityConstants.StatusWithinLimits, result.Status);
        _operationsMock.VerifyAll();
    }

    [Theory(DisplayName = "Chỉ LIFEGUARD được đo nước — ADMIN và role khác bị chặn")]
    [InlineData("ADMIN", false)]
    [InlineData("ADMIN", true)]
    [InlineData("SALES", false)]
    public async Task NonLifeguard_CannotCreateWaterQualityLog_ThrowsUnauthorizedAccessException(
        string role, bool hasOperationalTask)
    {
        _currentUserMock.Setup(user => user.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(user => user.IsInRole(role)).Returns(true);

        var command = new CreateWaterQualityLogCommand
        {
            OperationalTaskId = hasOperationalTask ? Guid.NewGuid() : null,
            PhValue = 7.4m,
            FreeChlorineMgL = 1.8m,
            IsWaterClear = true
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateHandler().Handle(command, CancellationToken.None));
        _operationsMock.Verify(operations => operations.CreateWaterQualityLogAsync(
            It.IsAny<WaterQualityLog>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<OperationalTaskExecutionAuthorization?>()), Times.Never);
    }

    [Fact(DisplayName = "UTC_CQ02 (N) - Chỉ số nguy hiểm (Danger) -> Phát cảnh báo thông báo và SignalR")]
    public async Task UTC_CQ02_DangerInput_TriggersDangerAlert()
    {
        var inspectorId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { Name = "ADMIN" }, IsDeleted = false };
        _userOperationsMock.Setup(u => u.GetUsersByRoleAsync("ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { adminUser });

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(It.IsAny<WaterQualityLog>(),
                It.IsAny<CancellationToken>(), It.IsAny<OperationalTaskExecutionAuthorization?>()))
            .ReturnsAsync((WaterQualityLog log, CancellationToken cancellationToken, OperationalTaskExecutionAuthorization? authorization) => new WaterQualityLogDto
            {
                Id = log.Id,
                InspectionTime = log.InspectionTime,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL,
                Status = log.Status,
                Recommendation = log.Recommendation
            });

        var handler = CreateHandler();
        var command = new CreateWaterQualityLogCommand
        {
            InspectionTime = DateTime.UtcNow,
            PhValue = 6.2m, // Danger: pH < 6.8
            FreeChlorineMgL = 0.2m, // Danger: Clo < 0.5
            IsWaterClear = false,
            Notes = "Nước hồ đổi màu"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(WaterQualityConstants.StatusDanger, result.Status);
        _notificationOperationsMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(notifs => notifs.Any(item =>
                item.UserId == adminUser.Id
                && item.Type == "WATER_QUALITY_ALERT"
                && item.Title.Contains("ĐÓNG CỬA", StringComparison.OrdinalIgnoreCase)
                && item.Content.Contains("xem xét tạm đóng cửa", StringComparison.OrdinalIgnoreCase))),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "WaterQualityUpdated",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToUserAsync(
            adminUser.Id.ToString(),
            "ReceiveNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "water_quality_danger",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Đo nước thành công -> phát SignalR WaterQualityUpdated")]
    public async Task CreateLog_Success_BroadcastsWaterQualityUpdatedSignalR()
    {
        var inspectorId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(
                It.Is<WaterQualityLog>(l => l.InspectedBy == inspectorId),
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _, OperationalTaskExecutionAuthorization? __) => new WaterQualityLogDto
            {
                Id = log.Id,
                Status = log.Status,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL
            });

        var result = await CreateHandler().Handle(new CreateWaterQualityLogCommand
        {
            PhValue = 7.4m,
            FreeChlorineMgL = 2.0m,
            IsWaterClear = true
        }, CancellationToken.None);

        Assert.Equal(WaterQualityConstants.StatusWithinLimits, result.Status);
        _operationsMock.Verify(o => o.CreateWaterQualityLogAsync(
            It.IsAny<WaterQualityLog>(),
            It.IsAny<CancellationToken>(),
            null), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "WaterQualityUpdated",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = "UTC_CQ03 (E) - pH vượt ngoài dải 0-14 -> Ném ValidationException")]
    [InlineData(-1.0)]
    [InlineData(14.5)]
    public async Task UTC_CQ03_InvalidPh_ThrowsValidationException(decimal invalidPh)
    {
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var handler = CreateHandler();
        var command = new CreateWaterQualityLogCommand
        {
            InspectionTime = DateTime.UtcNow,
            PhValue = invalidPh,
            FreeChlorineMgL = 2.0m
        };

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact(DisplayName = "UTC_WQ_TREND06 (N) - Khi phát hiện xu hướng đi xuống 3 ngày và sinh Lịch trực bể ngày mai -> Báo Admin phân công")]
    public async Task CreateLog_WhenThreeDayTrendDetected_NotifiesAdmins()
    {
        var inspectorId = Guid.NewGuid();
        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { Name = "ADMIN" }, IsDeleted = false };
        var nextDayDutyId = Guid.NewGuid();
        var nextDayDate = new DateOnly(2026, 11, 23);

        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _userOperationsMock.Setup(u => u.GetUsersByRoleAsync("ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync([adminUser]);

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(
                It.IsAny<WaterQualityLog>(),
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _, OperationalTaskExecutionAuthorization? __) => new WaterQualityLogDto
            {
                Id = log.Id,
                Status = log.Status,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL,
                ThreeDayTrendDetected = true,
                ThreeDayTrendSummary = "Xu hướng 3 ngày (20/11–22/11): pH giảm liên tục 3 ngày.",
                TrendSuggestedChemicals = ["Soda Ash (Na2CO3)", "Trợ lắng PAC / Phèn nhôm"],
                GeneratedNextDayDutyId = nextDayDutyId,
                GeneratedNextDayDutyDate = nextDayDate
            });

        var result = await CreateHandler().Handle(new CreateWaterQualityLogCommand
        {
            PhValue = 7.4m,
            FreeChlorineMgL = 2.0m,
            IsWaterClear = false
        }, CancellationToken.None);

        Assert.True(result.ThreeDayTrendDetected);
        Assert.Equal(nextDayDutyId, result.GeneratedNextDayDutyId);
        _notificationOperationsMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(n => n.Any(item => item.UserId == adminUser.Id && item.Type == "WATER_QUALITY_TREND_DUTY")),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToUserAsync(
            adminUser.Id.ToString(), "ReceiveNotification", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_WQ_TREND07 (N) - Xu hướng 3 ngày đi xuống kèm mức Danger -> Chỉ báo khẩn cấp, không tạo thông báo Lịch trực ngày mai")]
    public async Task CreateLog_WhenThreeDayTrendAndDanger_DoesNotSendNextDayTrendDutyNotice()
    {
        var inspectorId = Guid.NewGuid();
        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { Name = "ADMIN" }, IsDeleted = false };

        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _userOperationsMock.Setup(u => u.GetUsersByRoleAsync("ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync([adminUser]);

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(
                It.IsAny<WaterQualityLog>(),
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _, OperationalTaskExecutionAuthorization? __) => new WaterQualityLogDto
            {
                Id = log.Id,
                Status = log.Status,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL,
                ThreeDayTrendDetected = true,
                ThreeDayTrendSummary = "Xu hướng 3 ngày (20/11–22/11): pH giảm liên tục 3 ngày.",
                TrendSuggestedChemicals = ["Soda Ash (Na2CO3)"],
                GeneratedNextDayDutyId = null,
                GeneratedNextDayDutyDate = null
            });

        var result = await CreateHandler().Handle(new CreateWaterQualityLogCommand
        {
            PhValue = 6.5m,
            FreeChlorineMgL = 2.0m,
            IsWaterClear = true
        }, CancellationToken.None);

        Assert.True(result.ThreeDayTrendDetected);
        Assert.Null(result.GeneratedNextDayDutyId);
        _notificationOperationsMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(n => n.Any(item => item.UserId == adminUser.Id && item.Type == "WATER_QUALITY_ALERT")),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationOperationsMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(n => n.Any(item => item.Type == "WATER_QUALITY_TREND_DUTY")),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTC_WQ_TREND08 (N) - Xu hướng 3 ngày đi xuống kèm mức Warning -> Không gửi thông báo Lịch trực ngày mai")]
    public async Task CreateLog_WhenThreeDayTrendAndWarning_DoesNotSendNextDayTrendDutyNotice()
    {
        var inspectorId = Guid.NewGuid();
        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { Name = "ADMIN" }, IsDeleted = false };

        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _userOperationsMock.Setup(u => u.GetUsersByRoleAsync("ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync([adminUser]);

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(
                It.IsAny<WaterQualityLog>(),
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _, OperationalTaskExecutionAuthorization? __) => new WaterQualityLogDto
            {
                Id = log.Id,
                Status = log.Status,
                PhValue = log.PhValue,
                FreeChlorineMgL = log.FreeChlorineMgL,
                ThreeDayTrendDetected = true,
                ThreeDayTrendSummary = "Xu hướng 3 ngày (20/11–22/11): pH giảm liên tục 3 ngày.",
                TrendSuggestedChemicals = ["Soda Ash (Na2CO3)"],
                GeneratedNextDayDutyId = null,
                GeneratedNextDayDutyDate = null
            });

        var result = await CreateHandler().Handle(new CreateWaterQualityLogCommand
        {
            PhValue = 7.0m,
            FreeChlorineMgL = 2.0m,
            IsWaterClear = true
        }, CancellationToken.None);

        Assert.True(result.ThreeDayTrendDetected);
        Assert.Null(result.GeneratedNextDayDutyId);
        _notificationOperationsMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Notification>>(n => n.Any(item => item.Type == "WATER_QUALITY_TREND_DUTY")),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
