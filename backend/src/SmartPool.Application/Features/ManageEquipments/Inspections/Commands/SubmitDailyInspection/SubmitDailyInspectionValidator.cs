using FluentValidation;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.SubmitDailyInspection;

public sealed class SubmitDailyInspectionValidator : AbstractValidator<SubmitDailyInspectionCommand>
{
    public SubmitDailyInspectionValidator()
    {
        RuleFor(x => x.InspectorId)
            .NotEmpty()
            .WithMessage("Không xác định được người kiểm tra.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Danh sách kiểm tra không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.EquipmentId)
                .NotEmpty()
                .WithMessage("Mã thiết bị không hợp lệ.");

            item.RuleFor(i => i.Note)
                .NotEmpty()
                .When(i => !i.IsPassed)
                .WithMessage("Vui lòng nhập ghi chú sự cố cho thiết bị không đạt.");
        });
    }
}
