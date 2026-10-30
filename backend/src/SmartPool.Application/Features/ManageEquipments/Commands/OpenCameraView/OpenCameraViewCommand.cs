using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Application.Features.ManageEquipments.Commands.OpenCameraView;

public sealed class OpenCameraViewCommand : IRequest<CameraView>
{
    public Guid EquipmentId { get; init; }
    public CameraOffer Offer { get; init; } = null!;
    public string Owner { get; init; } = string.Empty;

    public OpenCameraViewCommand(Guid equipmentId, CameraOffer offer, string owner)
    {
        EquipmentId = equipmentId;
        Offer = offer;
        Owner = owner;
    }
}
