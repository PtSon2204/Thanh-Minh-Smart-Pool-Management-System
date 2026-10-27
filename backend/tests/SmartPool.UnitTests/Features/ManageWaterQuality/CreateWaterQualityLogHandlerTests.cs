using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Services;
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
    private readonly Mock<IRepository<Notification>> _notificationRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
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
            _notificationRepoMock.Object,
            _userRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    [Fact(DisplayName = "UTC_CQ01 (N) - Nhập chỉ số chuẩn -> Lưu phiếu thành công với Status = WithinLimits")]
    public async Task UTC_CQ01_ValidInput_CreatesLogWithWithinLimitsStatus()
    {
        var inspectorId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(It.IsAny<WaterQualityLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _) => new WaterQualityLogDto
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
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_CQ02 (N) - Chỉ số nguy hiểm (Danger) -> Phát cảnh báo thông báo và SignalR")]
    public async Task UTC_CQ02_DangerInput_TriggersDangerAlert()
    {
        var inspectorId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(inspectorId);

        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { Name = "ADMIN" }, IsDeleted = false };
        _userRepoMock.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { adminUser });

        _operationsMock.Setup(o => o.CreateWaterQualityLogAsync(It.IsAny<WaterQualityLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaterQualityLog log, CancellationToken _) => new WaterQualityLogDto
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
        _notificationRepoMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendToAllAsync(
            "water_quality_danger",
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
}
