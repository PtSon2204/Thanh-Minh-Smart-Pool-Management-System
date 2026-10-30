using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewConfig;

public sealed class GetCameraViewConfigHandler(
    IRepository<Equipment> repository,
    ICameraStreamService streams)
    : IRequestHandler<GetCameraViewConfigQuery, CameraWebRtcConfiguration>
{
    public async Task<CameraWebRtcConfiguration> Handle(GetCameraViewConfigQuery query, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(query.EquipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true)
            throw new KeyNotFoundException("Không tìm thấy camera");
        if (!CameraMetadata.IsCamera(equipment.Category))
            throw new InvalidOperationException("Thiết bị không thuộc nhóm Camera.");
        if (equipment.Status != "Active")
            throw new CameraConnectionException("Unavailable", "Camera hiện không khả dụng");
        return streams.GetConfiguration();
    }
}
