using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class DailyInspection
{
    public Guid Id { get; set; }

    public Guid InspectorId { get; set; }

    public Guid? ShiftId { get; set; }

    public DateOnly InspectionDate { get; set; }

    public DateTime InspectionTime { get; set; }

    /// <summary>
    /// JSON array of checked equipment items:
    /// [{ equipmentId, equipmentName, category, location, isPassed, note }]
    /// </summary>
    public string ItemsJson { get; set; } = "[]";

    /// <summary>
    /// Passed (Tất cả đạt) or HasIssues (Có thiết bị hỏng/cần bảo trì)
    /// </summary>
    public string OverallStatus { get; set; } = "Passed";

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User Inspector { get; set; } = null!;

    public virtual EmployeeShift? Shift { get; set; }
}
