namespace SmartPool.Application.Features.ManageEquipments.Cameras;

public sealed record CameraConnectionStatus(string State, DateTimeOffset? LastFrameAt)
{
    public string? AnalysisSession { get; init; }
    public bool SmoothPlayback { get; init; }
}
