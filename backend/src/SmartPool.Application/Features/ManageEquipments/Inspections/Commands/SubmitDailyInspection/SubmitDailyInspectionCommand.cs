using System;
using System.Collections.Generic;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;

public sealed class SubmitDailyInspectionCommand : IRequest<DailyInspectionDto>
{
    public Guid InspectorId { get; set; }
    public Guid ShiftId { get; set; }
    public string? Notes { get; set; }
    public List<DailyInspectionItemDto> Items { get; set; } = [];
}
