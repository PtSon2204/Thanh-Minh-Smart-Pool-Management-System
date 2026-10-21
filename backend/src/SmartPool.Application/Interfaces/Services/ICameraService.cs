using SmartPool.Application.Features.ManageEquipments.Cameras;
namespace SmartPool.Application.Interfaces.Services;

public interface ICameraService
{
    Task<string> GetIdentityAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken);
    Task<ICameraRegistration> PrepareAsync(CameraConnectionConfiguration configuration, CancellationToken cancellationToken);
    CameraConnectionStatus GetStatus(Guid equipmentId);
    byte[]? GetFrame(Guid equipmentId);
}
