using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetOperationCostsStatistics;

public sealed class OperationCostCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}
