using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

public sealed class IncidentCategoryDistributionDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}
