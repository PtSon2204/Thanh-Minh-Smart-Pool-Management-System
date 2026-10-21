namespace SmartPool.Application.Features.ManageEquipments.Cameras;
public sealed record CameraWebRtcConfiguration(IReadOnlyList<CameraIceServer> IceServers);
