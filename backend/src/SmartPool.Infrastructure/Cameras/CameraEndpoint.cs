namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraEndpoint
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 554;
    // Hostnames must have explicitly pinned addresses; no dynamic DNS destination is trusted.
    public List<string> Addresses { get; set; } = [];
}
