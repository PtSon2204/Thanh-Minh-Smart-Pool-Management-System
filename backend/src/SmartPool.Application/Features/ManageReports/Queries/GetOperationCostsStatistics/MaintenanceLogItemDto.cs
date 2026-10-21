using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetOperationCostsStatistics;

public sealed class MaintenanceLogItemDto
{
    public Guid Id { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string MaintenanceType { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string FormattedCost { get; set; } = string.Empty;
    public string ActualDate { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string Evaluation { get; set; } = string.Empty;
}
