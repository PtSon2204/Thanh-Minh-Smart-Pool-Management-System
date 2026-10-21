using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;

public sealed class RecentIncidentDto
{
    public Guid Id { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public string ReportedTime { get; set; } = string.Empty;
    public string HandlerStaffName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResolutionTime { get; set; } = string.Empty;
}
