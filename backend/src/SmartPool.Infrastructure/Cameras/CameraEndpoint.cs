namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraEndpoint
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 554;
    // Chỉ bật tường minh cho MediaMTX trong Development/Testing.
    public bool Simulator { get; set; }
    // Hostnames must have explicitly pinned addresses; no dynamic DNS destination is trusted.
    public List<string> Addresses { get; set; } = [];
}
