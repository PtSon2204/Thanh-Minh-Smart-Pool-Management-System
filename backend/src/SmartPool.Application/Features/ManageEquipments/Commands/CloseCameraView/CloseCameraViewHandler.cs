using MediatR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Commands.CloseCameraView;

public sealed class CloseCameraViewHandler(ICameraStreamService streams)
    : IRequestHandler<CloseCameraViewCommand, Unit>
{
    public async Task<Unit> Handle(CloseCameraViewCommand command, CancellationToken cancellationToken)
    {
        await streams.CloseAsync(command.EquipmentId, command.ViewId, command.Owner);
        return Unit.Value;
    }
}
