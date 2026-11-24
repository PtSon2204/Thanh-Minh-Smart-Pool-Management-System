namespace SmartPool.Application.Features.SafetyMonitoring;

public sealed record AlertDto(Guid Id, Guid? CameraId, string? CameraName,
    string? Location, string? TrackId, string? RiskEpisode, string Severity, string Status,
    string? ImageUrl, string SnapshotStatus, Guid? IncidentId, Guid? HandledBy, DateTime? HandledAt, DateTime CreatedAt,
    double[]? Box = null)
{
    public DateTime? ObservedAt { get; init; }
    public string? HandledByName { get; init; }
}

public sealed record AlertCameraOption(Guid Id, string Name);

public sealed record AlertFilterOptions(string[] Areas, string[] Severities, string[] Statuses,
    AlertCameraOption[] Cameras, string[] Dates);

public sealed record RiskWriteResult(AlertDto Alert);
