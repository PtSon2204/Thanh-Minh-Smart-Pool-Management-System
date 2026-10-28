using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Commands.OpenCameraView;

public sealed class OpenCameraViewHandler(
    IRepository<Equipment> repository,
    ICameraStreamService streams)
    : IRequestHandler<OpenCameraViewCommand, CameraView>
{
    public async Task<CameraView> Handle(OpenCameraViewCommand command, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(command.EquipmentId, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true)
            throw new KeyNotFoundException("Không tìm thấy camera");
        if (!CameraMetadata.IsCamera(equipment.Category))
            throw new InvalidOperationException("Thiết bị không thuộc nhóm Camera.");
        if (equipment.Status != "Active")
            throw new CameraConnectionException("Unavailable", "Camera hiện không khả dụng");
        return await streams.OpenAsync(command.EquipmentId, command.Owner, command.Offer, cancellationToken);
    }
}
