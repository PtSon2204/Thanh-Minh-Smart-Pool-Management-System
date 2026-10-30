using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

public sealed class DailyInspectionFormResponseDto
{
    public IReadOnlyList<DailyInspectionFormItemDto> Equipments { get; set; } = [];
    public IReadOnlyList<ShiftInspectionStatusDto> Shifts { get; set; } = [];
    public Guid? CurrentShiftId { get; set; }
    public bool CanSubmit { get; set; }
    public string? DisableReason { get; set; }
    public string ServerTime { get; set; } = string.Empty;
}
