using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewStatus;

public sealed class GetCameraViewStatusHandler(
    IRepository<Equipment> repository,
    ICameraStreamService streams)
    : IRequestHandler<GetCameraViewStatusQuery, CameraViewStatus>
{
    public async Task<CameraViewStatus> Handle(GetCameraViewStatusQuery query, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(query.EquipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true)
            throw new KeyNotFoundException("Không tìm thấy camera");
        if (!CameraMetadata.IsCamera(equipment.Category))
            throw new InvalidOperationException("Thiết bị không thuộc nhóm Camera.");
        if (equipment.Status != "Active")
            throw new CameraConnectionException("Unavailable", "Camera hiện không khả dụng");
        return await streams.GetStatusAsync(query.EquipmentId, query.ViewId, query.Owner, cancellationToken);
    }
}
