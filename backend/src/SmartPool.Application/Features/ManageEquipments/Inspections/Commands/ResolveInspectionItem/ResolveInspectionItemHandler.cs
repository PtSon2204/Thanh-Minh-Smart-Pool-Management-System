using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.ResolveInspectionItem;

public sealed class ResolveInspectionItemHandler(
    IRepository<DailyInspection> inspectionRepository,
    IRepository<Equipment> equipmentRepository,
    IRepository<User> userRepository,
    IUnitOfWork unitOfWork,
    IValidator<ResolveInspectionItemCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<ResolveInspectionItemCommand, DailyInspectionDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DailyInspectionDto> Handle(
        ResolveInspectionItemCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var inspection = await inspectionRepository.GetByIdAsync(command.InspectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy biên bản kiểm tra thiết bị.");

        var items = string.IsNullOrWhiteSpace(inspection.ItemsJson)
            ? []
            : JsonSerializer.Deserialize<List<DailyInspectionItemDto>>(inspection.ItemsJson, JsonOptions) ?? [];

        var targetItem = items.FirstOrDefault(i => i.EquipmentId == command.EquipmentId)
            ?? throw new ValidationException("Thiết bị không tồn tại trong danh sách kiểm tra của biên bản này.");

        if (targetItem.IsPassed)
        {
            throw new ValidationException("Thiết bị này hoạt động bình thường, không có sự cố cần xử lý.");
        }

        var admin = await userRepository.GetByIdAsync(command.AdminId, cancellationToken);
        var adminName = admin?.UserProfile?.FullName ?? admin?.Username ?? "Quản trị viên";
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Cập nhật chi tiết kết quả xử lý cho thiết bị
        targetItem.ResolutionStatus = command.Action;
        targetItem.ResolvedAt = nowUtc;
        targetItem.ResolvedBy = adminName;
        targetItem.ResolutionNote = command.Note?.Trim();

        // 2. Tính toán lại trạng thái tổng thể của toàn bộ biên bản
        var unresolvedCount = items.Count(i => !i.IsPassed && i.ResolutionStatus != "Repaired" && i.ResolutionStatus != "Replaced");
        inspection.OverallStatus = unresolvedCount == 0 ? "Resolved" : "InResolution";
        inspection.ItemsJson = JsonSerializer.Serialize(items);
        inspection.UpdatedAt = nowUtc;

        // 3. Hoàn trả trạng thái thiết bị trong bảng Equipment về Hoạt động (Active)
        var equipment = await equipmentRepository.GetByIdAsync(command.EquipmentId, cancellationToken);
        if (equipment != null)
        {
            equipment.Status = EquipmentStatuses.Active;
            equipment.UpdatedAt = nowUtc;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var shiftDef = inspection.ShiftId.HasValue ? FixedShifts.FindById(inspection.ShiftId.Value) : null;
        var inspector = await userRepository.GetByIdAsync(inspection.InspectorId, cancellationToken);
        var inspectorName = inspector?.UserProfile?.FullName ?? inspector?.Username ?? "Nhân viên";

        var sortedItems = items
            .OrderBy(x => x.IsPassed)
            .ThenBy(x => x.ResolutionStatus == "Repaired" || x.ResolutionStatus == "Replaced")
            .ThenBy(x => x.EquipmentName)
            .ToList();

        return new DailyInspectionDto
        {
            Id = inspection.Id,
            InspectorId = inspection.InspectorId,
            InspectorName = inspectorName,
            ShiftId = inspection.ShiftId ?? Guid.Empty,
            ShiftName = shiftDef?.Name ?? "Chưa phân ca",
            InspectionDate = inspection.InspectionDate,
            InspectionTime = inspection.InspectionTime,
            OverallStatus = inspection.OverallStatus,
            TotalItems = sortedItems.Count,
            IssuesCount = sortedItems.Count(i => !i.IsPassed),
            Notes = inspection.Notes,
            Items = sortedItems
        };
    }
}
