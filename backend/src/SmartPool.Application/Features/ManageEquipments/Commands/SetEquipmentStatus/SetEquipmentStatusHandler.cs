using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;

public sealed class SetEquipmentStatusHandler(IRepository<Equipment> repository, IUnitOfWork unitOfWork,
    IValidator<SetEquipmentStatusCommand> validator, TimeProvider timeProvider) : IRequestHandler<SetEquipmentStatusCommand, bool>
{
    public async Task<bool> Handle(SetEquipmentStatusCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var equipment = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return false;
        if (equipment.Status == request.Status) return true;
        equipment.Status = request.Status;
        equipment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        repository.Update(equipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
