using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class CustomerSegmentDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}
