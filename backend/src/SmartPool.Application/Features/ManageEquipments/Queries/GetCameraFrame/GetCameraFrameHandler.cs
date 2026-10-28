using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraFrame;

public sealed class GetCameraFrameHandler(
    IRepository<Equipment> repository,
    ICameraService cameras)
    : IRequestHandler<GetCameraFrameQuery, byte[]?>
{
    public async Task<byte[]?> Handle(GetCameraFrameQuery query, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(query.EquipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true || !CameraMetadata.IsCamera(equipment.Category))
            throw new KeyNotFoundException();
        return equipment.Status == "Active" ? cameras.GetFrame(query.EquipmentId) : null;
    }
}
