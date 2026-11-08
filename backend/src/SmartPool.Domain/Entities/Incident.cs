using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class Incident
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string SeverityLevel { get; set; } = null!;

    public Guid? ReportedBy { get; set; }

    public DateTime? IncidentTime { get; set; }
    //sonpt th�m field image_incident - 10/10/2026
    public string? ImageIncidents { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Giám sát an toàn — liên kết với cảnh báo và camera sinh ra sự cố
    public Guid? WarningId { get; set; }

    public Guid? CameraId { get; set; }

    public string? Location { get; set; }

    public virtual User? ReportedByNavigation { get; set; }
    public virtual Equipment? Camera { get; set; }
    public virtual Warning? Warning { get; set; }
}

