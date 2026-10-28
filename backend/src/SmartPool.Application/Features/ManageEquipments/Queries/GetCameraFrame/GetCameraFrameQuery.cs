using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraFrame;

/// <summary>Returns the raw JPEG frame bytes, or null if camera is offline / not active.</summary>
public sealed record GetCameraFrameQuery(Guid EquipmentId) : IRequest<byte[]?>;
