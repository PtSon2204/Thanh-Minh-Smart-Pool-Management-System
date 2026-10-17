using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;

public sealed class CreateEquipmentHandler(
    IRepository<Equipment> repository,
    IUnitOfWork unitOfWork,
    IValidator<CreateEquipmentCommand> validator,
    TimeProvider timeProvider,
    ICameraService? cameras = null,
    IEquipmentOperations? equipmentOperations = null) : IRequestHandler<CreateEquipmentCommand, EquipmentListItem>
{
    public async Task<EquipmentListItem> Handle(CreateEquipmentCommand request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var category = request.Category.Trim();
        if (!await repository.ExistsAsync(x => x.IsDeleted != true && x.Category == category, cancellationToken) &&
            await repository.ExistsAsync(x => x.IsDeleted != true && x.Category != "", cancellationToken))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.Category), "Nhóm thiết bị không tồn tại. Vui lòng chọn nhóm từ danh sách hiện có.")]);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var equipment = new Equipment
        {
            Id = Guid.NewGuid(), Name = request.Name.Trim(), Category = category,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Metadata = request.Metadata is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } metadata
                ? metadata.GetRawText() : null,
            Status = EquipmentStatuses.Active, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        };
        if (CameraMetadata.IsCamera(category))
        {
            if (cameras == null || equipmentOperations == null)
                throw new CameraConnectionException("NotConfigured", "Dịch vụ camera chưa được cấu hình.");
            var identity = await cameras.GetIdentityAsync(request.CameraConnection!, cancellationToken);
            if (await equipmentOperations.CameraExistsAsync(identity, cancellationToken))
                throw new CameraConnectionException("Duplicate", "Camera này đã được đăng ký trong hệ thống.");
            // Network work finishes BEFORE opening the short database transaction.
            await using var registration = await cameras.PrepareAsync(request.CameraConnection!, cancellationToken);
            equipment.Metadata = CameraMetadata.Pack(registration, equipment.Metadata);
            await equipmentOperations.RegisterCameraAsync(equipment, registration.Identity, () => registration.Publish(equipment.Id), cancellationToken);
            registration.Confirm();
        }
        else
        {
            await repository.AddAsync(equipment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return new EquipmentListItem
        {
            Id = equipment.Id, Name = equipment.Name, Category = equipment.Category, Location = equipment.Location,
            Metadata = CameraMetadata.PublicSpecifications(equipment.Metadata), Status = equipment.Status, NextMaintenanceDate = null,
            CameraConnection = CameraMetadata.IsCamera(category) ? cameras!.GetStatus(equipment.Id) : null
        };
    }
}
