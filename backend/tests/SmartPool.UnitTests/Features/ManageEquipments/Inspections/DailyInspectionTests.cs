using System.Linq.Expressions;
using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Features.ManageEquipments.Inspections;

public sealed class DailyInspectionTests
{
    [Fact(DisplayName = "Submit inspection khi tất cả thiết bị đạt -> Lưu trạng thái Passed, không khóa thiết bị")]
    public async Task SubmitInspection_AllPassed_ReturnsPassedStatus()
    {
        var inspectorId = Guid.NewGuid();
        var eqId = Guid.NewGuid();

        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var equipmentRepo = new Mock<IRepository<Equipment>>();
        var scheduleRepo = new Mock<IRepository<MaintenanceSchedule>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepo.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            equipmentRepo.Object,
            scheduleRepo.Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            TimeProvider.System);

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
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

        inspectionRepo.Verify(r => r.AddAsync(It.IsAny<DailyInspection>(), It.IsAny<CancellationToken>()), Times.Once);
        scheduleRepo.Verify(r => r.AddAsync(It.IsAny<MaintenanceSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Submit inspection có thiết bị hỏng -> Khóa thiết bị sang Maintenance và tạo MaintenanceSchedule")]
    public async Task SubmitInspection_HasBrokenEquipment_LocksEquipmentAndCreatesSchedule()
    {
        var inspectorId = Guid.NewGuid();
        var eqId = Guid.NewGuid();
        var equipment = new Equipment
        {
            Id = eqId,
            Name = "Phao cứu sinh số 2",
            Category = "Rescue",
            Status = EquipmentStatuses.Active,
            IsDeleted = false
        };

        var inspectionRepo = new Mock<IRepository<DailyInspection>>();
        var equipmentRepo = new Mock<IRepository<Equipment>>();
        var scheduleRepo = new Mock<IRepository<MaintenanceSchedule>>();
        var notifRepo = new Mock<IRepository<Notification>>();
        var userRepo = new Mock<IRepository<User>>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepo.Setup(r => r.GetByIdAsync(inspectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = inspectorId, Username = "lifeguard1" });
        userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { new() { Id = Guid.NewGuid(), Username = "admin" } });
        equipmentRepo.Setup(r => r.GetByIdAsync(eqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(equipment);

        var handler = new SubmitDailyInspectionHandler(
            inspectionRepo.Object,
            equipmentRepo.Object,
            scheduleRepo.Object,
            notifRepo.Object,
            userRepo.Object,
            unitOfWork.Object,
            new SubmitDailyInspectionValidator(),
            TimeProvider.System);

        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = inspectorId,
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
        Assert.Equal(EquipmentStatuses.Maintenance, equipment.Status);

        equipmentRepo.Verify(r => r.Update(equipment), Times.Once);
        scheduleRepo.Verify(r => r.AddAsync(It.Is<MaintenanceSchedule>(s => s.EquipmentId == eqId && s.Status == "Pending"), It.IsAny<CancellationToken>()), Times.Once);
        notifRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Validator từ chối khi thiết bị hỏng mà không ghi chú lý do")]
    public void Validator_RejectsFailedItemWithoutNote()
    {
        var validator = new SubmitDailyInspectionValidator();
        var command = new SubmitDailyInspectionCommand
        {
            InspectorId = Guid.NewGuid(),
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

    [Fact(DisplayName = "GetDailyInspectionForm trả danh sách thiết bị chưa bị xóa")]
    public async Task GetDailyInspectionForm_ReturnsNonDeletedEquipments()
    {
        var eqRepo = new Mock<IRepository<Equipment>>();
        eqRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipment>
            {
                new() { Id = Guid.NewGuid(), Name = "Camera 01", Category = "Camera", Status = "Active", IsDeleted = false },
                new() { Id = Guid.NewGuid(), Name = "Phao tròn", Category = "Rescue", Status = "Active", IsDeleted = false }
            });

        var handler = new GetDailyInspectionFormHandler(eqRepo.Object);
        var result = await handler.Handle(new GetDailyInspectionFormQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Name == "Camera 01");
        Assert.Contains(result, i => i.Name == "Phao tròn");
    }
}
