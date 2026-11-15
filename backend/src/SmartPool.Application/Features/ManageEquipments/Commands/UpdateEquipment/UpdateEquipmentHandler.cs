using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;

public sealed class UpdateEquipmentHandler(
    IEquipmentOperations equipmentOperations,
    IValidator<UpdateEquipmentCommand> validator,
    TimeProvider timeProvider) : IRequestHandler<UpdateEquipmentCommand, bool>
{
    public async Task<bool> Handle(UpdateEquipmentCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var equipment = await equipmentOperations.GetByIdAsync(request.Id, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return false;

        var category = request.Category.Trim();
        if (CameraMetadata.IsCamera(category) != CameraMetadata.IsCamera(equipment.Category))
            throw new ValidationException([new ValidationFailure(nameof(request.Category), "Không thể chuyển đổi giữa Camera và nhóm thiết bị khác.")]);

        equipment.Name = request.Name.Trim();
        equipment.Category = category;
        equipment.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        equipment.Metadata = CameraMetadata.UpdateSpecifications(equipment.Metadata, request.Metadata);
        if (!string.IsNullOrWhiteSpace(request.PriorityLevel))
        {
            var match = EquipmentPriorityLevels.All.FirstOrDefault(p => string.Equals(p, request.PriorityLevel.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                equipment.PriorityLevel = match;
            }
        }
        equipment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        // Updating public specifications preserves protected camera configuration and maintenance records.
        await equipmentOperations.UpdateAsync(equipment, cancellationToken);
        return true;
    }
}
