using System;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.ResolveInspectionItem;

public sealed class ResolveInspectionItemCommand : IRequest<DailyInspectionDto>
{
    public Guid InspectionId { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid AdminId { get; set; }
    public string Action { get; set; } = string.Empty; // "Repaired" hoặc "Replaced"
    public string? Note { get; set; }
}
