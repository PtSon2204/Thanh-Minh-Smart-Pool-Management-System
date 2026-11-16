using System;
using System.Collections.Generic;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;

public sealed class SubmitDailyInspectionCommand : IRequest<DailyInspectionDto>
{
    public Guid InspectorId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? OperationalTaskId { get; set; }
    public bool AdminExecutionConfirmed { get; set; }
    public long? ExpectedTaskRevision { get; set; }
    public Guid? ExpectedAssigneeId { get; set; }
    public string? Notes { get; set; }
    public List<DailyInspectionItemDto> Items { get; set; } = [];
}
