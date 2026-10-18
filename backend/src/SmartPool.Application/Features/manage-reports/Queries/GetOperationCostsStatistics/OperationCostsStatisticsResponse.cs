using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetOperationCostsStatistics;

public sealed class OperationCostsStatisticsResponse
{
    // === 4 THẺ KPI ===
    public decimal TotalCosts { get; set; }
    public decimal EquipmentMaintenanceCost { get; set; }
    public decimal WaterTreatmentCost { get; set; }
    public double CostToRevenueRatio { get; set; } // Tỷ lệ chi phí / doanh thu (%)

    // === BIỂU ĐỒ BIẾN ĐỘNG CHI PHÍ QUA CÁC THÁNG (STACKED BAR CHART) ===
    public List<OperationCostTrendDto> MonthlyTrends { get; set; } = new();

    // === BIỂU ĐỒ CƠ CẤU CÁC HẠNG MỤC CHI PHÍ (DONUT CHART) ===
    public List<OperationCostCategoryDto> CategoryDistributions { get; set; } = new();

    // === NHẬT KÝ CHI PHÍ BẢO TRÌ & SỬA CHỮA (TABLE) ===
    public List<MaintenanceLogItemDto> RecentMaintenanceLogs { get; set; } = new();
}

public sealed class OperationCostTrendDto
{
    public string Month { get; set; } = string.Empty;
    public decimal EquipmentCost { get; set; } // Bảo trì máy móc
    public decimal ChemicalCost { get; set; }  // Hóa chất xử lý nước
    public decimal ElectricityWaterCost { get; set; } // Điện nước vận hành
    public decimal TotalMonthlyCost { get; set; }
}

public sealed class OperationCostCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

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
