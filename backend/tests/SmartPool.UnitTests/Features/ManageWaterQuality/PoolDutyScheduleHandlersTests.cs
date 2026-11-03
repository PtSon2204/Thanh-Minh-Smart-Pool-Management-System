using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;
using SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class PoolDutyScheduleHandlersTests
{
    private readonly Mock<IWaterQualityOperations> _operationsMock = new();
    private readonly Mock<IRepository<Notification>> _notificationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<AutoAssignPoolDutyHandler>> _autoLoggerMock = new();
    private readonly Mock<ILogger<UpdatePoolDutyScheduleHandler>> _updateLoggerMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact(DisplayName = "UTC_PD01 (N) - Tự động gán lịch trực bể hợp lệ -> Phân công thành công và gửi thông báo")]
    public async Task UTC_PD01_AutoAssign_ValidCommand_ReturnsResultAndNotifiesStaff()
    {
        var emp1 = Guid.NewGuid();
        var emp2 = Guid.NewGuid();
        var fromDate = new DateOnly(2026, 11, 2);
        var toDate = new DateOnly(2026, 11, 8);

        var expectedResult = new AutoAssignPoolDutyResultDto
        {
            TotalSlotsProcessed = 14,
            CreatedCount = 14,
            UpdatedCount = 0,
            SkippedCount = 0,
            Message = "Đã tự động phân công thành công.",
            AssignedSchedules =
            [
                new PoolDutyScheduleDto
                {
                    Id = Guid.NewGuid(),
                    DutyDate = fromDate,
                    ShiftId = FixedShifts.Morning.Id,
                    ShiftName = FixedShifts.Morning.Name,
                    AssignedEmployeeId = emp1,
                    AssignedEmployeeName = "Nguyễn Văn A",
                    Status = "Scheduled",
                    AssignmentType = "Auto"
                },
                new PoolDutyScheduleDto
                {
                    Id = Guid.NewGuid(),
                    DutyDate = fromDate,
                    ShiftId = FixedShifts.Afternoon.Id,
                    ShiftName = FixedShifts.Afternoon.Name,
                    AssignedEmployeeId = emp2,
                    AssignedEmployeeName = "Trần Thị B",
                    Status = "Scheduled",
                    AssignmentType = "Auto"
                }
            ]
        };

        _operationsMock
            .Setup(o => o.AutoAssignPoolDutySchedulesAsync(It.IsAny<AutoAssignPoolDutyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new AutoAssignPoolDutyHandler(
            _operationsMock.Object,
            new AutoAssignPoolDutyValidator(),
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _autoLoggerMock.Object);

        var command = new AutoAssignPoolDutyCommand
        {
            FromDate = fromDate,
            ToDate = toDate,
            OverwriteExisting = false,
            PrioritizeShiftRoster = true
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(14, result.CreatedCount);
        Assert.Equal(2, result.AssignedSchedules.Count);
        _notificationRepoMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_PD02 (E) - Tự động gán lịch trực bể với ToDate < FromDate -> Ném ValidationException")]
    public async Task UTC_PD02_AutoAssign_InvalidDateRange_ThrowsValidationException()
    {
        var handler = new AutoAssignPoolDutyHandler(
            _operationsMock.Object,
            new AutoAssignPoolDutyValidator(),
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _autoLoggerMock.Object);

        var command = new AutoAssignPoolDutyCommand
        {
            FromDate = new DateOnly(2026, 11, 10),
            ToDate = new DateOnly(2026, 11, 5)
        };

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact(DisplayName = "UTC_PD03 (N) - Xem lịch trực bể -> Trả về danh sách lịch trực theo bộ lọc")]
    public async Task UTC_PD03_GetPoolDutySchedules_ReturnsPagedResponse()
    {
        var expected = new PagedResponse<PoolDutyScheduleDto>
        {
            Items =
            [
                new PoolDutyScheduleDto
                {
                    Id = Guid.NewGuid(),
                    DutyDate = new DateOnly(2026, 11, 2),
                    ShiftId = FixedShifts.Morning.Id,
                    ShiftName = "Ca sáng",
                    AssignedEmployeeName = "Lê Văn C",
                    Status = "Scheduled"
                }
            ],
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };

        _operationsMock
            .Setup(o => o.GetPoolDutySchedulesAsync(It.IsAny<GetPoolDutySchedulesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetPoolDutySchedulesHandler(_operationsMock.Object);
        var result = await handler.Handle(new GetPoolDutySchedulesQuery
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 7)
        }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Lê Văn C", result.Items[0].AssignedEmployeeName);
    }

    [Fact(DisplayName = "UTC_PD04 (N) - Cập nhật lịch trực bể hợp lệ -> Lưu thay đổi và gửi thông báo")]
    public async Task UTC_PD04_UpdatePoolDutySchedule_ValidInput_UpdatesSuccessfully()
    {
        var scheduleId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var dutyDate = new DateOnly(2026, 11, 3);

        _operationsMock
            .Setup(o => o.SavePoolDutyScheduleAsync(It.IsAny<UpdatePoolDutyScheduleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdatePoolDutyScheduleCommand cmd, CancellationToken _) => new PoolDutyScheduleDto
            {
                Id = cmd.Id ?? scheduleId,
                DutyDate = cmd.DutyDate,
                ShiftId = cmd.ShiftId,
                ShiftName = "Ca sáng",
                AssignedEmployeeId = cmd.AssignedEmployeeId,
                AssignedEmployeeName = "Phạm Văn D",
                Status = cmd.Status,
                AssignmentType = "Manual",
                Notes = cmd.Notes
            });

        var handler = new UpdatePoolDutyScheduleHandler(
            _operationsMock.Object,
            new UpdatePoolDutyScheduleValidator(),
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _updateLoggerMock.Object);

        var command = new UpdatePoolDutyScheduleCommand
        {
            Id = scheduleId,
            DutyDate = dutyDate,
            ShiftId = FixedShifts.Morning.Id,
            AssignedEmployeeId = employeeId,
            Status = "Completed",
            Notes = "Đã đo nước đầu ca và vệ sinh lòng bể"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(scheduleId, result.Id);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Manual", result.AssignmentType);
        _notificationRepoMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTC_PD05 (E) - Cập nhật lịch trực bể với trạng thái không hợp lệ -> Ném ValidationException")]
    public async Task UTC_PD05_UpdatePoolDutySchedule_InvalidStatus_ThrowsValidationException()
    {
        var handler = new UpdatePoolDutyScheduleHandler(
            _operationsMock.Object,
            new UpdatePoolDutyScheduleValidator(),
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _updateLoggerMock.Object);

        var command = new UpdatePoolDutyScheduleCommand
        {
            Id = Guid.NewGuid(),
            DutyDate = new DateOnly(2026, 11, 3),
            ShiftId = FixedShifts.Morning.Id,
            AssignedEmployeeId = Guid.NewGuid(),
            Status = "InvalidStatus"
        };

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }
}

