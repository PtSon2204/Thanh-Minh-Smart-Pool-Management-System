using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceRequestValidator : AbstractValidator<MaintenanceRequest>
{
    public MaintenanceRequestValidator()
    {
        RuleFor(x => x.EquipmentId).NotEmpty().WithMessage("Chọn thiết bị.");
        RuleFor(x => x.TaskName).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Nhập tên công việc.")
            .Must(x => x.Trim().Length <= 255 && !x.Contains('\0')).WithMessage("Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
        RuleFor(x => x.RepeatUnit).Must(x => x is "None" or "Day" or "Week" or "Month").WithMessage("Chọn chu kỳ bảo trì hợp lệ.");
        RuleFor(x => x.RepeatEvery).InclusiveBetween(1, 365).WithMessage("Chu kỳ lặp từ 1 đến 365.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Chọn ngày bắt đầu.");
        RuleFor(x => x.AssignedTo).NotEmpty().WithMessage("Chọn người phụ trách.");
        RuleFor(x => x.RemindBeforeDays).InclusiveBetween(0, 365).WithMessage("Nhắc trước từ 0 đến 365 ngày.");
    }
}
