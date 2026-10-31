using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;

public sealed class SubmitDailyInspectionHandler(
    IRepository<DailyInspection> inspectionRepository,
    IRepository<Equipment> equipmentRepository,
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
        var currentTime = VietnamTimeBoundary.GetTime(timeProvider);

        var shiftDef = FixedShifts.FindById(command.ShiftId)
            ?? throw new ValidationException("Ca trực không hợp lệ. Chỉ chấp nhận Ca sáng hoặc Ca chiều.");

        // Rule 1: Khung giờ kiểm tra — Chỉ mở trước ca trực 30 phút cho đến khi ca kết thúc
        if (!shiftDef.IsInInspectionWindow(currentTime))
        {
            if (currentTime < shiftDef.AllowedFromTime)
            {
                throw new ValidationException(
                    $"Chưa đến thời gian mở kiểm tra cho {shiftDef.Name}. " +
                    $"Hệ thống chỉ cho phép nộp biên bản trước ca trực 30 phút (từ {shiftDef.AllowedFromTime:HH\\:mm}).");
            }
            else
            {
                throw new ValidationException(
                    $"{shiftDef.Name} ({shiftDef.StartTime:HH\\:mm} - {shiftDef.EndTime:HH\\:mm}) đã kết thúc ca trực. " +
                    $"Không thể nộp biên bản kiểm tra cho ca này.");
            }
        }

        // Rule 2: Mỗi ca trực chỉ được gửi 1 lần duy nhất trong ngày
        var existingInspections = await inspectionRepository.FindAsync(
            x => x.InspectionDate == today && x.ShiftId == command.ShiftId,
            cancellationToken);

        var existing = existingInspections.FirstOrDefault();
        if (existing != null)
        {
            var prevInspector = await userRepository.GetByIdAsync(existing.InspectorId, cancellationToken);
            var prevInspectorName = prevInspector?.UserProfile?.FullName ?? prevInspector?.Username ?? "Cứu hộ";

            throw new ValidationException(
                $"{shiftDef.Name} ngày {today:dd/MM/yyyy} đã được hoàn thành kiểm tra lúc {existing.InspectionTime:HH\\:mm} bởi {prevInspectorName}. " +
                $"Mỗi ca trực chỉ được nộp biên bản kiểm tra 1 lần duy nhất.");
        }

        var failedItems = command.Items.Where(i => !i.IsPassed).ToList();
        var hasIssues = failedItems.Count > 0;
        var overallStatus = hasIssues ? "HasIssues" : "Passed";

        // Thiết lập trạng thái ban đầu cho các thiết bị gặp sự cố
        foreach (var item in failedItems)
        {
            item.ResolutionStatus = "Pending";
        }

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

        // 2. Tự động chuyển các thiết bị gặp sự cố sang trạng thái Bảo trì (Maintenance)
        if (hasIssues)
        {
            foreach (var failedItem in failedItems)
            {
                var equipment = await equipmentRepository.GetByIdAsync(failedItem.EquipmentId, cancellationToken);
                if (equipment != null)
                {
                    equipment.Status = EquipmentStatuses.Maintenance;
                    equipment.UpdatedAt = nowUtc;
                }
            }

            // 3. Notify Admins if there are failed items
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
                    Title = $"Cảnh báo sự cố thiết bị ({shiftDef.Name})",
                    Content = $"{inspectorName} đã kiểm tra đầu {shiftDef.Name.ToLower()} ngày {today:dd/MM/yyyy} và ghi nhận {failedItems.Count} thiết bị gặp sự cố.",
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
            ShiftId = inspection.ShiftId ?? command.ShiftId,
            ShiftName = shiftDef.Name,
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
