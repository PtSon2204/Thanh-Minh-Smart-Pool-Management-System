using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;

public sealed class SetEquipmentStatusValidator : AbstractValidator<SetEquipmentStatusCommand>
{
    public SetEquipmentStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).Must(value => value is EquipmentStatuses.Active or EquipmentStatuses.Inactive)
            .WithMessage("Trạng thái mở/khóa thiết bị không hợp lệ.");
    }
}
