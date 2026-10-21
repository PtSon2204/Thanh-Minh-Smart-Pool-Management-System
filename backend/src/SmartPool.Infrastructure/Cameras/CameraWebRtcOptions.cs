using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Infrastructure.Cameras;

public sealed class CameraWebRtcOptions
{
    public string? BindAddress { get; set; }
    public int PortRangeStart { get; set; } = 50000;
    public int PortRangeEnd { get; set; } = 50127;
    public List<CameraIceServer> IceServers { get; set; } = [];
}
