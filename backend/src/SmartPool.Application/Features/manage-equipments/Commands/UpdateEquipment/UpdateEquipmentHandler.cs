using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;

public sealed class UpdateEquipmentHandler(IRepository<Equipment> repository, IUnitOfWork unitOfWork,
    IValidator<UpdateEquipmentCommand> validator, TimeProvider timeProvider) : IRequestHandler<UpdateEquipmentCommand, bool>
{
    public async Task<bool> Handle(UpdateEquipmentCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var equipment = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (equipment == null || equipment.IsDeleted == true) return false;

        var category = request.Category.Trim();
        if (CameraMetadata.IsCamera(category) != CameraMetadata.IsCamera(equipment.Category))
            throw new ValidationException([new ValidationFailure(nameof(request.Category), "Không thể chuyển đổi giữa Camera và nhóm thiết bị khác.")]);
        if (category != equipment.Category && !await repository.ExistsAsync(x => x.IsDeleted != true && x.Category == category, cancellationToken))
            throw new ValidationException([new ValidationFailure(nameof(request.Category), "Nhóm thiết bị không tồn tại. Vui lòng chọn nhóm từ danh sách hiện có.")]);

        equipment.Name = request.Name.Trim();
        equipment.Category = category;
        equipment.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        equipment.Metadata = CameraMetadata.UpdateSpecifications(equipment.Metadata, request.Metadata);
        equipment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        // Updating public specifications preserves protected camera configuration and maintenance records.
        repository.Update(equipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
