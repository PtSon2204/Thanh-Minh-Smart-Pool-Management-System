using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetMandatoryChecklistStatus;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using Xunit;
using SmartPool.Domain.Constants;

namespace SmartPool.UnitTests.Features.ManageEquipments.Inspections;

public sealed class GetMandatoryChecklistStatusTests
{
    // 05:45 AM VN (22:45 UTC hôm trước) — Nằm trong khung giờ kiểm tra Ca sáng (05:30 - 11:30)
    private static readonly DateTimeOffset MorningInspectionTimeUtc =
        new(2026, 11, 19, 22, 45, 0, TimeSpan.Zero);

    // 04:00 AM VN (21:00 UTC hôm trước) — Ngoài khung giờ kiểm tra
    private static readonly DateTimeOffset OutsideInspectionTimeUtc =
        new(2026, 11, 19, 21, 0, 0, TimeSpan.Zero);

    private readonly Mock<IWaterQualityOperations> _waterQualityOps = new();
    private readonly Mock<IDailyInspectionOperations> _inspectionOps = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact(DisplayName = "Tài khoản không phải LIFEGUARD -> HasPendingMandatoryChecklist = false")]
    public async Task Handle_NonLifeguardUser_ReturnsFalse()
    {
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(u => u.IsInRole("LIFEGUARD")).Returns(false);

        var handler = new GetMandatoryChecklistStatusHandler(
            _waterQualityOps.Object,
            _inspectionOps.Object,
            _currentUser.Object,
            new MockTimeProvider(MorningInspectionTimeUtc));

        var result = await handler.Handle(new GetMandatoryChecklistStatusQuery(), CancellationToken.None);

        Assert.False(result.HasPendingMandatoryChecklist);
    }

    [Fact(DisplayName = "Cứu hộ không có task checklist hôm nay -> HasPendingMandatoryChecklist = false")]
    public async Task Handle_NoAssignedTasks_ReturnsFalse()
    {
        var lifeguardId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(lifeguardId);
        _currentUser.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        _waterQualityOps.Setup(w => w.GetOperationalTasksAsync(It.IsAny<GetOperationalTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<PoolDutyScheduleDto>
            {
                Items = new List<PoolDutyScheduleDto>(),
                TotalCount = 0,
                PageIndex = 1,
                PageSize = 10
            });

        var handler = new GetMandatoryChecklistStatusHandler(
            _waterQualityOps.Object,
            _inspectionOps.Object,
            _currentUser.Object,
            new MockTimeProvider(MorningInspectionTimeUtc));

        var result = await handler.Handle(new GetMandatoryChecklistStatusQuery(), CancellationToken.None);

        Assert.False(result.HasPendingMandatoryChecklist);
    }

    [Fact(DisplayName = "Cứu hộ có task nhưng ngoài khung giờ kiểm tra -> HasPendingMandatoryChecklist = false")]
    public async Task Handle_TaskOutsideInspectionWindow_ReturnsFalse()
    {
        var lifeguardId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(lifeguardId);
        _currentUser.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        var task = new PoolDutyScheduleDto
        {
            Id = taskId,
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            ShiftId = FixedShifts.Morning.Id,
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            Status = "Scheduled",
            AssignedEmployeeId = lifeguardId
        };

        _waterQualityOps.Setup(w => w.GetOperationalTasksAsync(It.IsAny<GetOperationalTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<PoolDutyScheduleDto>
            {
                Items = new List<PoolDutyScheduleDto> { task },
                TotalCount = 1,
                PageIndex = 1,
                PageSize = 10
            });

        var handler = new GetMandatoryChecklistStatusHandler(
            _waterQualityOps.Object,
            _inspectionOps.Object,
            _currentUser.Object,
            new MockTimeProvider(OutsideInspectionTimeUtc)); // 04:00 AM VN

        var result = await handler.Handle(new GetMandatoryChecklistStatusQuery(), CancellationToken.None);

        Assert.False(result.HasPendingMandatoryChecklist);
    }

    [Fact(DisplayName = "Cứu hộ có task trong khung giờ nhưng ca trực đã có biên bản nộp -> HasPendingMandatoryChecklist = false")]
    public async Task Handle_TaskAlreadySubmitted_ReturnsFalse()
    {
        var lifeguardId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(lifeguardId);
        _currentUser.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        var task = new PoolDutyScheduleDto
        {
            Id = taskId,
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            ShiftId = FixedShifts.Morning.Id,
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            Status = "Scheduled",
            AssignedEmployeeId = lifeguardId
        };

        _waterQualityOps.Setup(w => w.GetOperationalTasksAsync(It.IsAny<GetOperationalTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<PoolDutyScheduleDto>
            {
                Items = new List<PoolDutyScheduleDto> { task },
                TotalCount = 1,
                PageIndex = 1,
                PageSize = 10
            });

        // Đã có biên bản kiểm tra cho ca sáng hôm nay
        _inspectionOps.Setup(i => i.GetByDateAndShiftAsync(It.IsAny<DateOnly>(), FixedShifts.Morning.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyInspection { Id = Guid.NewGuid(), ShiftId = FixedShifts.Morning.Id });

        var handler = new GetMandatoryChecklistStatusHandler(
            _waterQualityOps.Object,
            _inspectionOps.Object,
            _currentUser.Object,
            new MockTimeProvider(MorningInspectionTimeUtc)); // 05:45 AM VN

        var result = await handler.Handle(new GetMandatoryChecklistStatusQuery(), CancellationToken.None);

        Assert.False(result.HasPendingMandatoryChecklist);
    }

    [Fact(DisplayName = "Cứu hộ có task trong khung giờ và chưa nộp -> HasPendingMandatoryChecklist = true")]
    public async Task Handle_TaskInWindowAndUnsubmitted_ReturnsTrue()
    {
        var lifeguardId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(lifeguardId);
        _currentUser.Setup(u => u.IsInRole("LIFEGUARD")).Returns(true);

        var task = new PoolDutyScheduleDto
        {
            Id = taskId,
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            ShiftId = FixedShifts.Morning.Id,
            ShiftName = "Ca sáng",
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            Status = "Scheduled",
            Revision = 1,
            AssignedEmployeeId = lifeguardId
        };

        _waterQualityOps.Setup(w => w.GetOperationalTasksAsync(It.IsAny<GetOperationalTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<PoolDutyScheduleDto>
            {
                Items = new List<PoolDutyScheduleDto> { task },
                TotalCount = 1,
                PageIndex = 1,
                PageSize = 10
            });

        // Chưa có biên bản
        _inspectionOps.Setup(i => i.GetByDateAndShiftAsync(It.IsAny<DateOnly>(), FixedShifts.Morning.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyInspection?)null);

        var handler = new GetMandatoryChecklistStatusHandler(
            _waterQualityOps.Object,
            _inspectionOps.Object,
            _currentUser.Object,
            new MockTimeProvider(MorningInspectionTimeUtc)); // 05:45 AM VN

        var result = await handler.Handle(new GetMandatoryChecklistStatusQuery(), CancellationToken.None);

        Assert.True(result.HasPendingMandatoryChecklist);
        Assert.Equal(taskId, result.TaskId);
        Assert.Equal(FixedShifts.Morning.Id, result.ShiftId);
        Assert.Equal("Ca sáng", result.ShiftName);
        Assert.Equal("05:30", result.AllowedFromTime);
        Assert.Equal("11:30", result.AllowedToTime);
        Assert.Equal(1, result.TaskRevision);
    }

    private sealed class MockTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
