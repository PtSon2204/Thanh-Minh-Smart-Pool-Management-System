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
    //sonpt thêm field image_incident - 10/10/2026
    public string? ImageIncidents { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ReportedByNavigation { get; set; }
}

