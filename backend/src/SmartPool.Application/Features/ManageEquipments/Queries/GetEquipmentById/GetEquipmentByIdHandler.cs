using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipmentById;

public sealed class GetEquipmentByIdHandler(
    IEquipmentOperations equipmentOperations,
    ICameraService? cameras = null)
    : IRequestHandler<GetEquipmentByIdQuery, EquipmentListItem?>
{
    public async Task<EquipmentListItem?> Handle(GetEquipmentByIdQuery request, CancellationToken cancellationToken)
    {
        var equipment = await equipmentOperations.GetByIdAsync(request.Id, cancellationToken);
        if (equipment is null || equipment.IsDeleted == true)
            return null;

        var isBrowserCamera = CameraMetadata.IsBrowserCamera(equipment.Metadata);
        return new EquipmentListItem
        {
            Id = equipment.Id,
            Name = equipment.Name,
            Category = equipment.Category,
            PriorityLevel = equipment.PriorityLevel ?? EquipmentPriorityLevels.Normal,
            Location = equipment.Location,
            Metadata = CameraMetadata.PublicSpecifications(equipment.Metadata),
            Status = equipment.Status,
            CameraSource = CameraMetadata.IsCamera(equipment.Category)
                ? isBrowserCamera ? "Browser" : "RTSP"
                : null,
            CameraConnection = CameraMetadata.IsCamera(equipment.Category) && !isBrowserCamera
                ? cameras?.GetStatus(equipment.Id) ?? new CameraConnectionStatus("Disconnected", null)
                : null
        };
    }
}
