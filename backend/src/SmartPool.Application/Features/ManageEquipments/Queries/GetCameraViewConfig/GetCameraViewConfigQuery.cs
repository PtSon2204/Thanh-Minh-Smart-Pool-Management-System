using MediatR;
using SmartPool.Application.Features.ManageEquipments.Cameras;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewConfig;

public sealed record GetCameraViewConfigQuery(Guid EquipmentId) : IRequest<CameraWebRtcConfiguration>;
