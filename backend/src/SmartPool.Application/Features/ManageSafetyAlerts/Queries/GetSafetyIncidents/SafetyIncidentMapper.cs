using System.Linq.Expressions;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed class SafetyIncidentRow
{
    public Incident Incident { get; init; } = null!;
    public User? User { get; init; }
    public UserProfile? Profile { get; init; }
}

// The same business projection serves detail mapping and SQL-backed list queries.
public static class SafetyIncidentMapper
{
    public static readonly Expression<Func<SafetyIncidentRow, SafetyIncidentDto>> Projection = row => new SafetyIncidentDto {
        Id = row.Incident.Id,
        IncidentCode = "INC-" + row.Incident.Id.ToString().Substring(0, 8).ToUpper(),
        Title = row.Incident.Title ?? "",
        Zone = !string.IsNullOrWhiteSpace(row.Incident.Location) ? row.Incident.Location :
            (((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("trẻ em") ||
             ((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("be tre em")) ? "Bể trẻ em" :
            (((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("bể lớn") ||
             ((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("be lon") ||
             ((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("bể chính") ||
             ((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("be chinh") ||
             ((row.Incident.Title ?? "") + " " + (row.Incident.Description ?? "")).ToLower().Contains("bể bơi chính")) ? "Bể lớn" : "Chưa xác định",
        SeverityLevel = row.Incident.SeverityLevel ?? "EMERGENCY",
        Status = string.IsNullOrWhiteSpace(row.Incident.Status) ? "Pending" :
            new[] { "pending", "chờ xử lý", "đang chờ" }.Contains(row.Incident.Status.Trim().ToLower()) ? "Pending" :
            new[] { "resolved", "đã giải quyết", "đã xử lý", "completed", "đã ghi nhận" }.Contains(row.Incident.Status.Trim().ToLower()) ? "Resolved" :
            new[] { "in_progress", "in progress", "đang xử lý", "processing" }.Contains(row.Incident.Status.Trim().ToLower()) ? "In Progress" : row.Incident.Status,
        Description = row.Incident.Description,
        IncidentTime = row.Incident.IncidentTime ?? row.Incident.CreatedAt ?? DateTime.UtcNow,
        ReportedByName = row.Incident.WarningId.HasValue || row.Incident.CameraId.HasValue ? "Hệ thống AI Camera" :
            row.Profile != null && !string.IsNullOrWhiteSpace(row.Profile.FullName) ? row.Profile.FullName :
            row.User != null && !string.IsNullOrWhiteSpace(row.User.Username) ? row.User.Username : "Nhân viên",
        ReportedByUserId = row.Incident.ReportedBy,
        ImageIncidents = row.Incident.ImageIncidents,
        SourceType = row.Incident.WarningId.HasValue || row.Incident.CameraId.HasValue ? "Camera AI giám sát" : "Cứu hộ viên phát hiện thực tế",
        UpdatedAt = row.Incident.UpdatedAt,
    };
    private static readonly Func<SafetyIncidentRow, SafetyIncidentDto> Map = Projection.Compile();
    public static SafetyIncidentDto ToDto(Incident incident, User? user, UserProfile? profile) =>
        Map(new() { Incident = incident, User = user, Profile = profile });
}
