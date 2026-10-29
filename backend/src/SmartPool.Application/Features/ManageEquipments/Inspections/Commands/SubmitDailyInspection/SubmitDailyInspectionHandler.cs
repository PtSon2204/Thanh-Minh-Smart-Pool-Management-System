using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;

public sealed class SubmitDailyInspectionHandler(
    IRepository<DailyInspection> inspectionRepository,
    IRepository<Equipment> equipmentRepository,
    IRepository<MaintenanceSchedule> scheduleRepository,
    IRepository<Notification> notificationRepository,
    IRepository<User> userRepository,
    IUnitOfWork unitOfWork,
    IValidator<SubmitDailyInspectionCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitDailyInspectionCommand, DailyInspectionDto>
{
    public async Task<DailyInspectionDto> Handle(
        SubmitDailyInspectionCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = VietnamTimeBoundary.GetDate(timeProvider);

        var failedItems = command.Items.Where(i => !i.IsPassed).ToList();
        var hasIssues = failedItems.Count > 0;
        var overallStatus = hasIssues ? "HasIssues" : "Passed";

        // Fetch inspector info
        var inspector = await userRepository.GetByIdAsync(command.InspectorId, cancellationToken);
        var inspectorName = inspector?.UserProfile?.FullName
            ?? inspector?.Username
            ?? "Cứu hộ";

        // 1. Create and persist DailyInspection record
        var inspection = new DailyInspection
        {
            Id = Guid.NewGuid(),
            InspectorId = command.InspectorId,
            ShiftId = command.ShiftId,
            InspectionDate = today,
            InspectionTime = nowUtc,
            ItemsJson = JsonSerializer.Serialize(command.Items),
            OverallStatus = overallStatus,
            Notes = command.Notes?.Trim(),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        await inspectionRepository.AddAsync(inspection, cancellationToken);

        // 2. Closed-loop flow: For each failed item, lock device and create maintenance task
        if (hasIssues)
        {
            foreach (var item in failedItems)
            {
                var equipment = await equipmentRepository.GetByIdAsync(item.EquipmentId, cancellationToken);
                if (equipment != null && equipment.IsDeleted != true)
                {
                    // Lock equipment status
                    equipment.Status = EquipmentStatuses.Maintenance;
                    equipment.UpdatedAt = nowUtc;
                    equipmentRepository.Update(equipment);

                    // Create maintenance schedule
                    var config = new MaintenanceConfiguration
                    {
                        SeriesId = Guid.NewGuid(),
                        Sequence = 0,
                        AnchorSequence = 0,
                        AnchorDate = today,
                        TaskName = $"[Sự cố đầu ca] {equipment.Name}: {item.Note?.Trim()}",
                        RepeatUnit = "None",
                        RepeatEvery = 1,
                        RemindBeforeDays = 0,
                        PreviousEquipmentStatus = EquipmentStatuses.Active
                    };

                    var schedule = new MaintenanceSchedule
                    {
                        Id = Guid.NewGuid(),
                        EquipmentId = equipment.Id,
                        AssignedTo = null,
                        PlannedDate = today,
                        Description = config.Serialize(),
                        Status = "Pending",
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };

                    await scheduleRepository.AddAsync(schedule, cancellationToken);
                }
            }

            // 3. Notify Admins
            var admins = await userRepository.FindAsync(
                u => u.IsDeleted != true && u.Role != null && u.Role.Name.ToUpper() == "ADMIN",
                cancellationToken);

            foreach (var admin in admins)
            {
                var notif = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
                    Type = $"INSP:{inspection.Id:N}",
                    Title = "Cảnh báo thiết bị gặp sự cố đầu ca",
                    Content = $"{inspectorName} đã kiểm tra đầu ca ngày {today:dd/MM/yyyy} và phát hiện {failedItems.Count} thiết bị gặp sự cố. Các thiết bị đã được tự động khóa để bảo trì.",
                    IsRead = false,
                    CreatedAt = nowUtc
                };
                await notificationRepository.AddAsync(notif, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DailyInspectionDto
        {
            Id = inspection.Id,
            InspectorId = inspection.InspectorId,
            InspectorName = inspectorName,
            ShiftId = inspection.ShiftId,
            InspectionDate = inspection.InspectionDate,
            InspectionTime = inspection.InspectionTime,
            OverallStatus = inspection.OverallStatus,
            TotalItems = command.Items.Count,
            IssuesCount = failedItems.Count,
            Notes = inspection.Notes,
            Items = command.Items
        };
    }
}
