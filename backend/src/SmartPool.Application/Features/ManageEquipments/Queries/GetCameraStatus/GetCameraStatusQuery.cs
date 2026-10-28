using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraStatus;

public sealed record GetCameraStatusQuery(Guid EquipmentId) : IRequest<CameraConnectionStatus>;
