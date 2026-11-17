namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraOptions
{
    public List<CameraEndpoint> AllowedEndpoints { get; set; } = [];
    public string FfmpegPath { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int FrameStaleSeconds { get; set; } = 15;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int SimulatorReconnectDelayMilliseconds { get; set; } = 250;
    public int AnalysisMaxWidth { get; set; } = 1280;
    public int AnalysisMaxHeight { get; set; } = 720;
    // FFmpeg JPEG qscale: giá trị thấp cho ảnh chất lượng cao hơn.
    public int AnalysisJpegQscale { get; set; } = 2;
    public int MaxConnections { get; set; } = 16;
    public CameraWebRtcOptions WebRtc { get; set; } = new();
    // Optional structured pipeline diagnostics. Disabled by default; the bounded
    // ring buffer and sampled events only run when explicitly enabled.
    public SmartPool.Infrastructure.SafetyMonitoring.CameraDiagnosticsOptions Diagnostics { get; set; }
        = new();

    public TimeSpan GetReconnectDelay(bool simulator) => simulator
        ? TimeSpan.FromMilliseconds(Math.Clamp(SimulatorReconnectDelayMilliseconds, 100, 2000))
        : TimeSpan.FromSeconds(Math.Clamp(ReconnectDelaySeconds, 1, 60));
}
