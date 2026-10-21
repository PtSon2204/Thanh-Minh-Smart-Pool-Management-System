using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;

public sealed class TicketTypeRevenueDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Count { get; set; }
}
