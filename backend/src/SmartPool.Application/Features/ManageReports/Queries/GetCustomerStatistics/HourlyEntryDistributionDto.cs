using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class HourlyEntryDistributionDto
{
    public string Hour { get; set; } = string.Empty;
    public int Count { get; set; }
}
