using Moq;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetTodayWaterQualityGate;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class GetTodayWaterQualityGateHandlerTests
{
    private readonly Mock<IWaterQualityOperations> _operationsMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();

    [Fact(DisplayName = "UTC_WQ_GATE01 (N) - Chưa đăng nhập hoặc thiếu UserId -> Không khóa, trả về RequiresMeasurement = false")]
    public async Task Handle_WhenUserIdMissing_ReturnsUnlockedWithoutQueryingRepository()
    {
        _currentUserMock.SetupGet(u => u.UserId).Returns((Guid?)null);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        var handler = new GetTodayWaterQualityGateHandler(_operationsMock.Object, _currentUserMock.Object);
        var result = await handler.Handle(new GetTodayWaterQualityGateQuery(), CancellationToken.None);

        Assert.False(result.RequiresMeasurement);
        Assert.Null(result.DutyDate);
        Assert.Null(result.TodayLogId);
        _operationsMock.Verify(o => o.GetTodayWaterQualityGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = "UTC_WQ_GATE02 (N) - Vai trò khác LIFEGUARD (ADMIN, SALES, COACH) -> Không bị chặn đo nước")]
    [InlineData("ADMIN")]
    [InlineData("SALES")]
    [InlineData("COACH")]
    public async Task Handle_WhenNonLifeguard_ReturnsUnlockedWithoutQueryingRepository(string role)
    {
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _currentUserMock.SetupGet(u => u.Role).Returns(role);
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(false);

        var handler = new GetTodayWaterQualityGateHandler(_operationsMock.Object, _currentUserMock.Object);
        var result = await handler.Handle(new GetTodayWaterQualityGateQuery(), CancellationToken.None);

        Assert.False(result.RequiresMeasurement);
        _operationsMock.Verify(o => o.GetTodayWaterQualityGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTC_WQ_GATE03 (N) - LIFEGUARD có lịch trực hôm nay & chưa đo nước -> Trả về RequiresMeasurement = true")]
    public async Task Handle_WhenLifeguardHasDutyAndNoLogToday_ReturnsLockedGate()
    {
        var employeeId = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 19);
        var expected = new TodayWaterQualityGateDto(
            true,
            today,
            null,
            "Bạn được phân công lịch trực/đo nước ngày 19/11/2026. Vui lòng hoàn thành đo chất lượng nước trước khi thực hiện các công việc khác.");

        _currentUserMock.SetupGet(u => u.UserId).Returns(employeeId);
        _currentUserMock.SetupGet(u => u.Role).Returns("LIFEGUARD");
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _operationsMock
            .Setup(o => o.GetTodayWaterQualityGateAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetTodayWaterQualityGateHandler(_operationsMock.Object, _currentUserMock.Object);
        var result = await handler.Handle(new GetTodayWaterQualityGateQuery(), CancellationToken.None);

        Assert.True(result.RequiresMeasurement);
        Assert.Equal(today, result.DutyDate);
        Assert.Null(result.TodayLogId);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        _operationsMock.Verify(o => o.GetTodayWaterQualityGateAsync(employeeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_WQ_GATE04 (N) - LIFEGUARD đã đo nước trong ngày hoặc không có ca hôm nay -> Mở khóa công việc khác")]
    public async Task Handle_WhenLifeguardAlreadyMeasuredOrNoDuty_ReturnsUnlockedGate()
    {
        var employeeId = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 19);
        var logId = Guid.NewGuid();
        var expected = new TodayWaterQualityGateDto(false, today, logId, null);

        _currentUserMock.SetupGet(u => u.UserId).Returns(employeeId);
        _currentUserMock.SetupGet(u => u.Role).Returns("LIFEGUARD");
        _currentUserMock.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);
        _operationsMock
            .Setup(o => o.GetTodayWaterQualityGateAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetTodayWaterQualityGateHandler(_operationsMock.Object, _currentUserMock.Object);
        var result = await handler.Handle(new GetTodayWaterQualityGateQuery(), CancellationToken.None);

        Assert.False(result.RequiresMeasurement);
        Assert.Equal(today, result.DutyDate);
        Assert.Equal(logId, result.TodayLogId);
        Assert.Null(result.Reason);
    }
}
