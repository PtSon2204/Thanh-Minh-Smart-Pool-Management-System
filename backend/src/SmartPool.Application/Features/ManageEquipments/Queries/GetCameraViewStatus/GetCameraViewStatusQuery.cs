using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewStatus;

public sealed record GetCameraViewStatusQuery(Guid EquipmentId, Guid ViewId, string Owner) : IRequest<CameraViewStatus>;
