using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;

public sealed class MonthlyTrendDto
{
    public string Month { get; set; } = string.Empty; // e.g. "T05", "T06"
    public int NewMembers { get; set; }
    public int RenewedMembers { get; set; }
}
