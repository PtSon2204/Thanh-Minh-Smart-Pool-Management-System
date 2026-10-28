using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraStatus;

public sealed class GetCameraStatusHandler(
    IRepository<Equipment> repository,
    ICameraService cameras)
    : IRequestHandler<GetCameraStatusQuery, CameraConnectionStatus>
{
    public async Task<CameraConnectionStatus> Handle(GetCameraStatusQuery query, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(query.EquipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true || !CameraMetadata.IsCamera(equipment.Category))
            throw new KeyNotFoundException();
        return cameras.GetStatus(query.EquipmentId);
    }
}
