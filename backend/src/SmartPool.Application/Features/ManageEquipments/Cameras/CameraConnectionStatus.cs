namespace SmartPool.Application.Features.ManageEquipments.Cameras;

public sealed record CameraConnectionStatus(string State, DateTimeOffset? LastFrameAt);
