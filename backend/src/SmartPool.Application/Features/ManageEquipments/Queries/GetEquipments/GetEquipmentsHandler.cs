using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;

// Application service for UC 8.1, following the existing MediatR handler convention.
public sealed class GetEquipmentsHandler(IEquipmentOperations repository, IValidator<GetEquipmentsQuery> validator, TimeProvider timeProvider, ICameraService? cameras = null)
    : IRequestHandler<GetEquipmentsQuery, PagedResponse<EquipmentListItem>>
{
    public async Task<PagedResponse<EquipmentListItem>> Handle(GetEquipmentsQuery request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await repository.GetListAsync(request, VietnamTimeBoundary.GetDate(timeProvider), cancellationToken);
        foreach (var equipment in result.Items)
        {
            var browser = CameraMetadata.IsBrowserCamera(equipment.Metadata);
            equipment.Metadata = CameraMetadata.PublicSpecifications(equipment.Metadata);
            if (CameraMetadata.IsCamera(equipment.Category))
            {
                equipment.CameraSource = browser ? "Browser" : "RTSP";
                if (!browser) equipment.CameraConnection = cameras?.GetStatus(equipment.Id) ?? new CameraConnectionStatus("Disconnected", null);
            }
        }
        return result;
    }
}
