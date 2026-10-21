using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;

public sealed class MemberTicketWarningDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
    public string Status { get; set; } = string.Empty; // "ACTIVE", "EXPIRING_SOON", "EXPIRED"
    public int TotalEntriesUsed { get; set; }
}
