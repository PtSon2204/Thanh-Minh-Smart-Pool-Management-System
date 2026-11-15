using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

internal static class SafetyIncidentMapper
{
    public static SafetyIncidentDto ToDto(Incident incident, User? reportedBy, UserProfile? profile)
    {
        var fromCamera = incident.WarningId.HasValue || incident.CameraId.HasValue;
        var reportedByName = fromCamera
            ? "Hệ thống AI Camera"
            : !string.IsNullOrWhiteSpace(profile?.FullName)
                ? profile.FullName
                : !string.IsNullOrWhiteSpace(reportedBy?.Username)
                    ? reportedBy.Username
                    : "Nhân viên";

        return new SafetyIncidentDto
        {
            Id = incident.Id,
            IncidentCode = $"INC-{incident.Id.ToString()[..8].ToUpperInvariant()}",
            Title = incident.Title,
            Zone = !string.IsNullOrWhiteSpace(incident.Location)
                ? incident.Location
                : ResolveZone(incident.Title, incident.Description),
            SeverityLevel = incident.SeverityLevel ?? "EMERGENCY",
            Status = NormalizeStatus(incident.Status),
            Description = incident.Description,
            IncidentTime = incident.IncidentTime ?? incident.CreatedAt ?? DateTime.UtcNow,
            ReportedByName = reportedByName,
            ReportedByUserId = incident.ReportedBy,
            ImageIncidents = incident.ImageIncidents,
            SourceType = fromCamera ? "Camera AI giám sát" : "Cứu hộ viên phát hiện thực tế",
            UpdatedAt = incident.UpdatedAt,
        };
    }

    private static string ResolveZone(string? title, string? description)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        if (text.Contains("trẻ em") || text.Contains("be tre em")) return "Bể trẻ em";
        if (text.Contains("bể lớn") || text.Contains("be lon") || text.Contains("bể chính") ||
            text.Contains("be chinh") || text.Contains("bể bơi chính")) return "Bể lớn";
        return "Chưa xác định";
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Pending";
        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is "pending" or "chờ xử lý" or "đang chờ") return "Pending";
        if (normalized is "resolved" or "đã giải quyết" or "đã xử lý" or "completed" or "đã ghi nhận")
            return "Resolved";
        if (normalized is "in_progress" or "in progress" or "đang xử lý" or "processing") return "In Progress";
        return status;
    }
}
