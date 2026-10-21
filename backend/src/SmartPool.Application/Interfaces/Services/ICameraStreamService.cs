using SmartPool.Application.Features.ManageEquipments.Cameras;
namespace SmartPool.Application.Interfaces.Services;

public interface ICameraStreamService
{
    CameraWebRtcConfiguration GetConfiguration();
    Task<CameraView> OpenAsync(Guid equipmentId, string owner, CameraOffer offer, CancellationToken cancellationToken);
    Task<CameraViewStatus> GetStatusAsync(Guid equipmentId, Guid viewId, string owner, CancellationToken cancellationToken);
    Task CloseAsync(Guid equipmentId, Guid viewId, string owner);
}
