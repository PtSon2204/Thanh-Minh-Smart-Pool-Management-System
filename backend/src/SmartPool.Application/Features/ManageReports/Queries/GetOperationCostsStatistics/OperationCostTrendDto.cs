using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetOperationCostsStatistics;

public sealed class OperationCostTrendDto
{
    public string Month { get; set; } = string.Empty;
    public decimal EquipmentCost { get; set; } // Bảo trì máy móc
    public decimal ChemicalCost { get; set; }  // Hóa chất xử lý nước
    public decimal ElectricityWaterCost { get; set; } // Điện nước vận hành
    public decimal TotalMonthlyCost { get; set; }
}
