using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed class TopCustomerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string TicketType { get; set; } = "Vé lượt";
    public int TotalEntries { get; set; }
    public decimal TotalSpending { get; set; }
    public string LastVisit { get; set; } = string.Empty;
}
