using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Commands.CloseCameraView;

public sealed record CloseCameraViewCommand(Guid EquipmentId, Guid ViewId, string Owner) : IRequest<Unit>;
