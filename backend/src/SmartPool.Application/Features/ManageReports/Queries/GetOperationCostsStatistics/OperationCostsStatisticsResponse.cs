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
