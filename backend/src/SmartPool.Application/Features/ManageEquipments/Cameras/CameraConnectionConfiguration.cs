namespace SmartPool.Application.Features.ManageEquipments.Cameras;

public sealed class CameraConnectionConfiguration
{
    public string Protocol { get; set; } = "RTSP";
    public string StreamUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
