using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;

public sealed class SetEquipmentStatusHandler(
    IEquipmentOperations equipmentOperations,
    IValidator<SetEquipmentStatusCommand> validator,
    TimeProvider timeProvider) : IRequestHandler<SetEquipmentStatusCommand, bool>
{
    public async Task<bool> Handle(SetEquipmentStatusCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var equipment = await equipmentOperations.GetByIdAsync(request.Id, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return false;
        if (equipment.Status == request.Status) return true;
        equipment.Status = request.Status;
        equipment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await equipmentOperations.UpdateAsync(equipment, cancellationToken);
        return true;
    }
}
