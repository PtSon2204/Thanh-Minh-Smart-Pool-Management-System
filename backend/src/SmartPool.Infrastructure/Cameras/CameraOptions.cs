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

public sealed class CameraWebRtcOptions
{
    public string? BindAddress { get; set; }
    public int PortRangeStart { get; set; } = 50000;
    public int PortRangeEnd { get; set; } = 50127;
    public List<SmartPool.Application.Interfaces.Services.CameraIceServer> IceServers { get; set; } = [];
}

public sealed class CameraEndpoint
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 554;
    // Hostnames must have explicitly pinned addresses; no dynamic DNS destination is trusted.
    public List<string> Addresses { get; set; } = [];
}
