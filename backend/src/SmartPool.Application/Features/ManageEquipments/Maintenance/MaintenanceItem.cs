using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed record MaintenanceItem(Guid Id, Guid EquipmentId, string EquipmentName, string? Location,
    string TaskName, string RepeatUnit, int RepeatEvery, DateOnly StartDate, Guid? AssignedTo,
    string AssigneeName, int RemindBeforeDays, string Status, bool IsOverdue, DateOnly? NextDate);
