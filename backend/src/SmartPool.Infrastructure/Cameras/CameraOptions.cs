namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraOptions
{
    public List<CameraEndpoint> AllowedEndpoints { get; set; } = [];
    public string FfmpegPath { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int FrameStaleSeconds { get; set; } = 15;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int MaxConnections { get; set; } = 16;
    public CameraWebRtcOptions WebRtc { get; set; } = new();
}
