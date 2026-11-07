using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
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
    TimeProvider timeProvider,
    IEmailService? emailService = null,
    ILogger<SubmitDailyInspectionHandler>? logger = null)
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

        // Lấy mức độ ưu tiên trực tiếp từ cấu hình của từng thiết bị trong cơ sở dữ liệu
        foreach (var item in command.Items)
        {
            var eq = await equipmentRepository.GetByIdAsync(item.EquipmentId, cancellationToken);
            if (eq != null && !string.IsNullOrWhiteSpace(eq.PriorityLevel))
            {
                item.PriorityLevel = eq.PriorityLevel;
            }
            else if (string.IsNullOrWhiteSpace(item.PriorityLevel))
            {
                item.PriorityLevel = SmartPool.Domain.Enums.EquipmentPriorityLevels.Normal;
            }

            if (!item.IsPassed)
            {
                item.ResolutionStatus = "Pending";
            }
        }

        var failedItems = command.Items.Where(i => !i.IsPassed).ToList();
        var criticalFailed = failedItems.Where(i => string.Equals(i.PriorityLevel, SmartPool.Domain.Enums.EquipmentPriorityLevels.Critical, StringComparison.OrdinalIgnoreCase)).ToList();
        var hasIssues = failedItems.Count > 0;
        var hasCriticalSafetyViolation = criticalFailed.Count > 0;
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

            var notifType = hasCriticalSafetyViolation ? $"SAFETY_ALERT:{inspection.Id:N}" : $"INSP:{inspection.Id:N}";
            var notifTitle = hasCriticalSafetyViolation
                ? $"⚠️ CẢNH BÁO ĐỎ AN TOÀN HỒ BƠI ({shiftDef.Name})"
                : $"Cảnh báo sự cố thiết bị ({shiftDef.Name})";
            var notifContent = hasCriticalSafetyViolation
                ? $"{inspectorName} ghi nhận {criticalFailed.Count} thiết bị cốt lõi ({string.Join(", ", criticalFailed.Select(c => c.EquipmentName))}) bị hư hỏng, vi phạm an toàn pháp lý! Đề xuất Admin kiểm tra và xác nhận tạm dừng hồ bơi."
                : $"{inspectorName} đã kiểm tra đầu {shiftDef.Name.ToLower()} ngày {today:dd/MM/yyyy} và ghi nhận {failedItems.Count} thiết bị gặp sự cố.";

            foreach (var admin in admins)
            {
                var notif = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
                    Type = notifType,
                    Title = notifTitle,
                    Content = notifContent,
                    IsRead = false,
                    CreatedAt = nowUtc
                };
                await notificationRepository.AddAsync(notif, cancellationToken);
            }

            // 4. Gửi email cảnh báo kèm ảnh chụp trực tiếp cho tất cả Admin
            var adminEmails = admins
                .Where(a => !string.IsNullOrWhiteSpace(a.Email))
                .Select(a => a.Email!.Trim())
                .Distinct()
                .ToList();

            if (adminEmails.Count > 0 && emailService != null)
            {
                try
                {
                    var subject = hasCriticalSafetyViolation
                        ? $"[CẢNH BÁO ĐỎ AN TOÀN] Phát hiện thiết bị cốt lõi hư hỏng - Ca {shiftDef.Name} ({today:dd/MM/yyyy})"
                        : $"[BÁO CÁO SỰ CỐ THIẾT BỊ] Ca {shiftDef.Name} ngày {today:dd/MM/yyyy}";

                    var itemsHtml = string.Join("", failedItems.Select(f =>
                    {
                        var priorityBadge = f.PriorityLevel == "Critical"
                            ? "<span style='background:#fee2e2;color:#b91c1c;padding:3px 8px;border-radius:4px;font-weight:bold;font-size:11px;'>NGUY CẤP</span>"
                            : f.PriorityLevel == "High"
                            ? "<span style='background:#fef3c7;color:#b45309;padding:3px 8px;border-radius:4px;font-weight:bold;font-size:11px;'>QUAN TRỌNG</span>"
                            : "<span style='background:#f3f4f6;color:#4b5563;padding:3px 8px;border-radius:4px;font-size:11px;'>THƯỜNG</span>";

                        var imagesHtml = f.DefectImageUrls is { Count: > 0 }
                            ? string.Join(" ", f.DefectImageUrls.Select(img => $"<a href='{img}' target='_blank'><img src='{img}' style='width:90px;height:90px;object-fit:cover;border-radius:6px;border:1px solid #e5e7eb;margin-right:6px;' alt='Ảnh lỗi'/></a>"))
                            : "<em style='color:#9ca3af;'>Chưa có ảnh</em>";

                        return $@"
                            <tr style='border-bottom:1px solid #e5e7eb;'>
                                <td style='padding:10px;font-weight:bold;color:#1f2937;'>{f.EquipmentName}</td>
                                <td style='padding:10px;color:#4b5563;'>{f.Category}</td>
                                <td style='padding:10px;color:#4b5563;'>{f.Location ?? "Khu vực chung"}</td>
                                <td style='padding:10px;'>{priorityBadge}</td>
                                <td style='padding:10px;color:#dc2626;'>{f.Note ?? "Không có mô tả"}</td>
                                <td style='padding:10px;'>{imagesHtml}</td>
                            </tr>";
                    }));

                    var alertHeaderBg = hasCriticalSafetyViolation ? "#dc2626" : "#f59e0b";
                    var alertHeaderText = hasCriticalSafetyViolation
                        ? "⚠️ CẢNH BÁO NGUY CẤP: PHÁT HIỆN SỰ CỐ THIẾT BỊ AN TOÀN PHÁP LÝ HỒ BƠI"
                        : "THÔNG BÁO GHI NHẬN THIẾT BỊ GẶP SỰ CỐ ĐẦU CA";

                    var htmlBody = $@"
                        <div style='font-family:Arial,sans-serif;max-width:750px;margin:0 auto;border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;'>
                            <div style='background:{alertHeaderBg};color:#ffffff;padding:16px 20px;text-align:center;'>
                                <h2 style='margin:0;font-size:18px;'>{alertHeaderText}</h2>
                                <p style='margin:6px 0 0;font-size:14px;opacity:0.95;'>Hệ thống SmartPool Management - Bể bơi Thành Minh</p>
                            </div>
                            <div style='padding:20px;background:#ffffff;'>
                                <p style='font-size:14px;color:#374151;line-height:1.6;'>
                                    Kính gửi Ban Quản lý / Quản trị viên,<br/>
                                    Nhân viên kiểm tra <strong>{inspectorName}</strong> vừa nộp biên bản kiểm tra đầu <strong>{shiftDef.Name}</strong> ngày <strong>{today:dd/MM/yyyy}</strong> lúc <strong>{nowUtc:HH:mm}</strong> (giờ UTC) và ghi nhận <strong>{failedItems.Count} thiết bị gặp sự cố</strong>.
                                </p>
                                {(hasCriticalSafetyViolation ? @"
                                <div style='background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;margin:16px 0;border-radius:4px;'>
                                    <strong style='color:#b91c1c;'>LƯU Ý ĐẶC BIỆT:</strong>
                                    <span style='color:#991b1b;'> Có thiết bị cốt lõi (Máy bơm lọc, Phao cứu sinh, Sào cứu hộ) bị hỏng, ảnh hưởng trực tiếp đến an toàn tính mạng khách bơi theo quy chuẩn pháp luật. Đề xuất Admin kiểm tra ngay và cân nhắc bấm tạm dừng hoạt động bể nếu không đảm bảo điều kiện vận hành.</span>
                                </div>" : "")}
                                <table style='width:100%;border-collapse:collapse;margin:16px 0;font-size:13px;'>
                                    <thead>
                                        <tr style='background:#f9fafb;border-bottom:2px solid #e5e7eb;text-align:left;color:#374151;'>
                                            <th style='padding:10px;'>Thiết bị</th>
                                            <th style='padding:10px;'>Nhóm</th>
                                            <th style='padding:10px;'>Vị trí</th>
                                            <th style='padding:10px;'>Mức ưu tiên</th>
                                            <th style='padding:10px;'>Mô tả hư hỏng</th>
                                            <th style='padding:10px;'>Hình ảnh hiện trường</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {itemsHtml}
                                    </tbody>
                                </table>
                                <div style='margin-top:24px;text-align:center;'>
                                    <p style='color:#6b7280;font-size:12px;'>Hệ thống sẽ định kỳ nhắc nhở mỗi 2 tiếng qua email cho đến khi Admin xác nhận hoàn tất xử lý các thiết bị này.</p>
                                </div>
                            </div>
                        </div>";

                    await emailService.SendAsync(adminEmails, subject, htmlBody, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Lỗi gửi email cảnh báo sự cố kiểm tra thiết bị cho Admin.");
                }
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
