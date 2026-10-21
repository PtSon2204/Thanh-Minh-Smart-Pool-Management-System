using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class CustomerDailyTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int NewUsers { get; set; }
    public int SwimEntries { get; set; }
}
