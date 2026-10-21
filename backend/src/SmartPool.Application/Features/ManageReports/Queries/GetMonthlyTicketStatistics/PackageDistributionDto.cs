using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;

public sealed class PackageDistributionDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = "#8b5cf6";
}
