using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageEquipments.Inspections;

public sealed class DailyInspectionTests
{
    // Giả lập thời gian thử nghiệm: 05:45 AM VN (22:45 UTC hôm trước) — Hợp lệ cho Ca sáng (mở từ 05:30)
    private static readonly DateTimeOffset MorningTestTimeUtc =
        new(2026, 10, 27, 22, 45, 0, TimeSpan.Zero);

    // Giả lập thời gian thử nghiệm: 04:00 AM VN (21:00 UTC hôm trước) — Quá sớm cho Ca sáng
    private static readonly DateTimeOffset TooEarlyTestTimeUtc =
        new(2026, 10, 27, 21, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Submit inspection trong khung giờ hợp lệ khi tất cả thiết bị đạt -> Lưu trạng thái Passed")]
    public async Task SubmitInspection_AllPassed_ReturnsPassedStatus()
    {
        var inspectorId = Guid.NewGuid();
        var eqId = Guid.NewGuid();

        var inspectionOps = new Mock<IDailyInspectionOperations>();
        var eqOps = new Mock<IEquipmentOperations>();
        var notifOps = new Mock<INotificationOperations>();
        var userOps = new Mock<IUserOperations>();

        userOps.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });

        inspectionOps.Setup(r => r.GetByDateAndShiftAsync(It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyInspection?)null);

        var handler = new SubmitDailyInspectionHandler(
            inspectionOps.Object,
            eqOps.Object,
            notifOps.Object,
            userOps.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc));

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
            ShiftId = FixedShifts.Morning.Id,
            Items =
            [
                new DailyInspectionItemDto
                {
                    EquipmentId = eqId,
                    EquipmentName = "Phao cứu sinh số 1",
                    Category = "Rescue",
                    IsPassed = true
                }
            ]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Passed", result.OverallStatus);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(0, result.IssuesCount);
        Assert.Equal("Ca sáng", result.ShiftName);

        inspectionOps.Verify(r => r.AddAsync(It.IsAny<DailyInspection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Submit inspection có thiết bị hỏng -> Lưu trạng thái HasIssues và thông báo Admin")]
    public async Task SubmitInspection_HasBrokenEquipment_SavesIssuesAndNotifiesAdmin()
    {
        var inspectorId = Guid.NewGuid();
        var eqId = Guid.NewGuid();

        var inspectionOps = new Mock<IDailyInspectionOperations>();
        var eqOps = new Mock<IEquipmentOperations>();
        var notifOps = new Mock<INotificationOperations>();
        var userOps = new Mock<IUserOperations>();

        userOps.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });
        userOps.Setup(r => r.GetUsersByRoleAsync("ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { new() { Id = Guid.NewGuid(), Username = "admin" } });

        inspectionOps.Setup(r => r.GetByDateAndShiftAsync(It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyInspection?)null);

        var handler = new SubmitDailyInspectionHandler(
            inspectionOps.Object,
            eqOps.Object,
            notifOps.Object,
            userOps.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc));

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
            ShiftId = FixedShifts.Morning.Id,
            Items =
            [
                new DailyInspectionItemDto
                {
                    EquipmentId = eqId,
                    EquipmentName = "Phao cứu sinh số 2",
                    Category = "Rescue",
                    IsPassed = false,
                    Note = "Phao bị rách van xịt hơi"
                }
            ]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("HasIssues", result.OverallStatus);
        Assert.Equal(1, result.IssuesCount);

        notifOps.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Từ chối nộp biên bản khi chưa đến giờ mở (trước ca 30 phút)")]
    public async Task SubmitInspection_TooEarly_ThrowsValidationException()
    {
        var inspectionOps = new Mock<IDailyInspectionOperations>();
        var eqOps = new Mock<IEquipmentOperations>();
        var notifOps = new Mock<INotificationOperations>();
        var userOps = new Mock<IUserOperations>();

        var handler = new SubmitDailyInspectionHandler(
            inspectionOps.Object,
            eqOps.Object,
            notifOps.Object,
            userOps.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(TooEarlyTestTimeUtc)); // 04:00 sáng

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
            ShiftId = FixedShifts.Morning.Id,
            Items =
            [
                new DailyInspectionItemDto
                {
                    EquipmentId = Guid.NewGuid(),
                    EquipmentName = "Camera 01",
                    IsPassed = true
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("Chưa đến thời gian mở kiểm tra", ex.Message);
    }

    [Fact(DisplayName = "Từ chối nộp biên bản trùng lặp nếu ca trực hôm nay đã được nộp rồi")]
    public async Task SubmitInspection_DuplicateShiftSubmission_ThrowsValidationException()
    {
        var inspectorId = Guid.NewGuid();
        var shiftId = FixedShifts.Morning.Id;

        var inspectionOps = new Mock<IDailyInspectionOperations>();
        var eqOps = new Mock<IEquipmentOperations>();
        var notifOps = new Mock<INotificationOperations>();
        var userOps = new Mock<IUserOperations>();

        // Giả lập đã có bản ghi trong DB cho ca sáng hôm nay
        inspectionOps.Setup(r => r.GetByDateAndShiftAsync(It.IsAny<DateOnly>(), shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyInspection
            {
                Id = Guid.NewGuid(),
                InspectorId = inspectorId,
                ShiftId = shiftId,
                InspectionDate = new DateOnly(2026, 10, 28),
                InspectionTime = new DateTime(2026, 10, 28, 5, 40, 0)
            });

        userOps.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });

        var handler = new SubmitDailyInspectionHandler(
            inspectionOps.Object,
            eqOps.Object,
            notifOps.Object,
            userOps.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc));

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
            ShiftId = shiftId,
            Items =
            [
                new DailyInspectionItemDto
                {
                    EquipmentId = Guid.NewGuid(),
                    EquipmentName = "Phao 01",
                    IsPassed = true
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("Mỗi ca trực chỉ được nộp biên bản kiểm tra 1 lần duy nhất", ex.Message);
    }

    [Fact(DisplayName = "Validator từ chối khi không chọn ca trực hoặc chọn ca không hợp lệ")]
    public void Validator_RejectsEmptyOrInvalidShiftId()
    {
        var validator = new SubmitDailyInspectionValidator();

        var emptyShiftCommand = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
            ShiftId = Guid.Empty,
            Items = [new DailyInspectionItemDto { EquipmentId = Guid.NewGuid(), IsPassed = true }]
        };
        var result1 = validator.Validate(emptyShiftCommand);
        Assert.False(result1.IsValid);
        Assert.Contains(result1.Errors, e => e.PropertyName == "ShiftId");

        var randomShiftCommand = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(), // Not in FixedShifts.Ids
            Items = [new DailyInspectionItemDto { EquipmentId = Guid.NewGuid(), IsPassed = true }]
        };
        var result2 = validator.Validate(randomShiftCommand);
        Assert.False(result2.IsValid);
        Assert.Contains(result2.Errors, e => e.ErrorMessage.Contains("Ca trực không hợp lệ"));
    }

    [Fact(DisplayName = "Validator từ chối khi thiết bị hỏng mà không ghi chú lý do")]
    public void Validator_RejectsFailedItemWithoutNote()
    {
        var validator = new SubmitDailyInspectionValidator();
        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
            ShiftId = FixedShifts.Morning.Id,
            Items =
            [
                new DailyInspectionItemDto
                {
                    EquipmentId = Guid.NewGuid(),
                    EquipmentName = "Camera 01",
                    IsPassed = false,
                    Note = "" // Missing note
                }
            ]
        };

        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("ghi chú"));
    }

    [Fact(DisplayName = "GetDailyInspectionForm trả danh sách thiết bị và trạng thái ca trực")]
    public async Task GetDailyInspectionForm_ReturnsEquipmentsAndShifts()
    {
        var eqOps = new Mock<IEquipmentOperations>();
        var inspOps = new Mock<IDailyInspectionOperations>();
        var userOps = new Mock<IUserOperations>();

        eqOps.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipment>
            {
                new() { Id = Guid.NewGuid(), Name = "Camera 01", Category = "Camera", Status = "Active", IsDeleted = false },
                new() { Id = Guid.NewGuid(), Name = "Phao tròn", Category = "Rescue", Status = "Active", IsDeleted = false }
            });

        inspOps.Setup(r => r.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>());

        var handler = new GetDailyInspectionFormHandler(
            eqOps.Object,
            inspOps.Object,
            userOps.Object,
            new TestTimeProvider(MorningTestTimeUtc)); // 05:45 AM VN

        var result = await handler.Handle(new GetDailyInspectionFormQuery(), CancellationToken.None);

        Assert.Equal(2, result.Equipments.Count);
        Assert.Equal(2, result.Shifts.Count);
        Assert.True(result.CanSubmit);
        Assert.Equal(FixedShifts.Morning.Id, result.CurrentShiftId);
        Assert.True(result.Shifts[0].IsOpenNow); // Ca sáng đang mở lúc 05:45
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
