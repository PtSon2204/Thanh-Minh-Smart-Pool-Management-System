using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;
using SmartPool.Application.Features.ManageWaterQuality.OperationalTasks;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
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

        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var eqRepo = new Mock<IRepository<Equipment>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(inspectorId);

        userRepo.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });

        inspectionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DailyInspection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>());

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            eqRepo.Object,
            new Mock<IRepository<PoolDutySchedule>>().Object,
            new Mock<IWaterQualityOperations>().Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc),
            currentUser.Object);

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

        inspectionRepo.Verify(r => r.AddAsync(It.IsAny<DailyInspection>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmed_admin_task_submission_uses_authenticated_actor_instead_of_client_inspector()
    {
        var adminId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = new PoolDutySchedule
        {
            Id = taskId,
            TaskType = PoolDutyTaskTypes.EquipmentChecklist,
            InspectionWindow = PoolDutyInspectionWindows.Morning,
            DutyDate = new DateOnly(2026, 10, 28),
            Status = "Unassigned",
            Revision = 4
        };
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(adminId);
        currentUser.Setup(user => user.IsInRole("ADMIN")).Returns(true);
        var taskTransaction = new Mock<IUnitOfWorkTransaction>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(taskTransaction.Object);
        var inspectionRepository = new Mock<IRepository<DailyInspection>>();
        inspectionRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<DailyInspection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>());
        var taskRepository = new Mock<IRepository<PoolDutySchedule>>();
        taskRepository.Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        var operations = new Mock<IWaterQualityOperations>();
        operations.Setup(repository => repository.CompleteOperationalChecklistTaskAsync(
                It.IsAny<DailyInspection>(), It.IsAny<OperationalTaskExecutionAuthorization>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var userRepository = new Mock<IRepository<User>>();
        userRepository.Setup(repository => repository.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = adminId, Username = "admin" });

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepository.Object,
            new Mock<IRepository<Equipment>>().Object,
            taskRepository.Object,
            operations.Object,
            new Mock<IRepository<Notification>>().Object,
            userRepository.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc),
            currentUser.Object);
        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
            ShiftId = FixedShifts.Morning.Id,
            OperationalTaskId = taskId,
            AdminExecutionConfirmed = true,
            ExpectedTaskRevision = 4,
            ExpectedAssigneeId = null,
            Items = [new DailyInspectionItemDto { EquipmentId = Guid.NewGuid(), EquipmentName = "Camera", IsPassed = true }]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(adminId, result.InspectorId);
        operations.Verify(repository => repository.CompleteOperationalChecklistTaskAsync(
            It.Is<DailyInspection>(inspection => inspection.InspectorId == adminId && inspection.OperationalTaskId == taskId),
            It.Is<OperationalTaskExecutionAuthorization>(authorization => authorization.IsAdmin &&
                authorization.AdminExecutionConfirmed && authorization.ExpectedTaskRevision == 4 &&
                authorization.ExpectedAssigneeId == null), It.IsAny<CancellationToken>()), Times.Once);
        taskTransaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Submit inspection có thiết bị hỏng -> Lưu trạng thái HasIssues và thông báo Admin")]
    public async Task SubmitInspection_HasBrokenEquipment_SavesIssuesAndNotifiesAdmin()
    {
        var inspectorId = Guid.NewGuid();
        var eqId = Guid.NewGuid();

        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var eqRepo = new Mock<IRepository<Equipment>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(inspectorId);

        userRepo.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });
        userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { new() { Id = Guid.NewGuid(), Username = "admin" } });

        inspectionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DailyInspection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>());

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            eqRepo.Object,
            new Mock<IRepository<PoolDutySchedule>>().Object,
            new Mock<IWaterQualityOperations>().Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc),
            currentUser.Object);

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

        notifRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Từ chối nộp biên bản khi chưa đến giờ mở (trước ca 30 phút)")]
    public async Task SubmitInspection_TooEarly_ThrowsValidationException()
    {
        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var eqRepo = new Mock<IRepository<Equipment>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var inspectorId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(inspectorId);

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            eqRepo.Object,
            new Mock<IRepository<PoolDutySchedule>>().Object,
            new Mock<IWaterQualityOperations>().Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(TooEarlyTestTimeUtc), // 04:00 sáng
            currentUser.Object);

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
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

        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var eqRepo = new Mock<IRepository<Equipment>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(inspectorId);

        // Giả lập đã có bản ghi trong DB cho ca sáng hôm nay
        inspectionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DailyInspection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    InspectorId = inspectorId,
                    ShiftId = shiftId,
                    InspectionDate = new DateOnly(2026, 10, 28),
                    InspectionTime = new DateTime(2026, 10, 28, 5, 40, 0)
                }
            });

        userRepo.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            eqRepo.Object,
            new Mock<IRepository<PoolDutySchedule>>().Object,
            new Mock<IWaterQualityOperations>().Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            new TestTimeProvider(MorningTestTimeUtc),
            currentUser.Object);

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
        var eqRepo = new Mock<IRepository<Equipment>>();
        var inspRepo = new Mock<IRepository<DailyInspection>>();
        var userRepo = new Mock<IRepository<User>>();

        eqRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipment>
            {
                new() { Id = Guid.NewGuid(), Name = "Camera 01", Category = "Camera", Status = "Active", IsDeleted = false },
                new() { Id = Guid.NewGuid(), Name = "Phao tròn", Category = "Rescue", Status = "Active", IsDeleted = false }
            });

        inspRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DailyInspection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyInspection>());

        var handler = new GetDailyInspectionFormHandler(
            eqRepo.Object,
            inspRepo.Object,
            userRepo.Object,
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
