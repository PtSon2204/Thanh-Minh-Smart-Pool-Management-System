using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed record MaintenanceHistoryItem(Guid Id, Guid? ScheduleId, Guid EquipmentId, string EquipmentName,
    string TaskName, DateOnly? PlannedDate, DateTime ActualDate, decimal Cost, string PerformedBy,
    string? VendorName, string? Note);
