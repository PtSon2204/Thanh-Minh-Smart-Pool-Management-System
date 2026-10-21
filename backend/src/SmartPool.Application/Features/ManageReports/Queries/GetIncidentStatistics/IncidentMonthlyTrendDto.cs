using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

public sealed class IncidentMonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
}
