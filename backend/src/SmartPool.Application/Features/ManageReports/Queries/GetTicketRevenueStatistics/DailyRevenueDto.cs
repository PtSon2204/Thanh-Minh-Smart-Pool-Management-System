using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;

public sealed class DailyRevenueDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TicketCount { get; set; }
}
