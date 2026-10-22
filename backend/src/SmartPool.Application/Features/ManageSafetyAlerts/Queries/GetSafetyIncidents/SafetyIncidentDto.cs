using System;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed record SafetyIncidentDto
{
    public Guid Id { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Zone { get; init; } = string.Empty;
    public string SeverityLevel { get; init; } = "EMERGENCY";
    public string Status { get; init; } = "Resolved";
    public string? Description { get; init; }
    public DateTime IncidentTime { get; init; }
    public string ReportedByName { get; init; } = string.Empty;
    public Guid? ReportedByUserId { get; init; }
    public string? ImageIncidents { get; init; }
    public string SourceType { get; init; } = "Cứu hộ viên";
    public DateTime? UpdatedAt { get; init; }
    public string? ResolutionNote { get; init; }
}
