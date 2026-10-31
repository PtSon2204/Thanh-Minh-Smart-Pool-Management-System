using FluentValidation;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Commands.ResolveInspectionItem;

public sealed class ResolveInspectionItemValidator : AbstractValidator<ResolveInspectionItemCommand>
{
    public ResolveInspectionItemValidator()
    {
        RuleFor(x => x.InspectionId)
            .NotEmpty().WithMessage("ID biên bản kiểm tra không được để trống.");

        RuleFor(x => x.EquipmentId)
            .NotEmpty().WithMessage("ID thiết bị không được để trống.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Vui lòng chọn hình thức xử lý.")
            .Must(a => a is "Repaired" or "Replaced")
            .WithMessage("Hình thức xử lý không hợp lệ. Chỉ chấp nhận 'Repaired' (Đã sửa chữa) hoặc 'Replaced' (Đã thay thế).");

        When(x => !string.IsNullOrEmpty(x.Note), () =>
        {
            RuleFor(x => x.Note)
                .MaximumLength(500).WithMessage("Ghi chú xử lý không được vượt quá 500 ký tự.");
        });
    }
}
