namespace SmartPool.Application.Interfaces.Services;

public sealed record CameraView(Guid ViewId, string Type, string Sdp);
public sealed record CameraOffer(string Type, string Sdp);
public sealed record CameraIceServer(string Urls, string? Username = null, string? Credential = null);
public sealed record CameraWebRtcConfiguration(IReadOnlyList<CameraIceServer> IceServers);
public sealed record CameraViewStatus(string State, string Codec, bool Transcoding);

public interface ICameraStreamService
{
    CameraWebRtcConfiguration GetConfiguration();
    Task<CameraView> OpenAsync(Guid equipmentId, string owner, CameraOffer offer, CancellationToken cancellationToken);
    Task<CameraViewStatus> GetStatusAsync(Guid equipmentId, Guid viewId, string owner, CancellationToken cancellationToken);
    Task CloseAsync(Guid equipmentId, Guid viewId, string owner);
}
