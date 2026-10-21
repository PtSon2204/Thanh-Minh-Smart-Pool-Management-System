using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveShift
{
    public sealed class SaveShiftValidator : AbstractValidator<SaveShiftCommand>
    {
        public SaveShiftValidator()
        {
            RuleFor(item => item.Name).NotEmpty().MaximumLength(100).WithMessage("Tên ca làm việc không hợp lệ.");
            RuleFor(item => item).Must(item => item.StartTime != item.EndTime).WithMessage("Giờ bắt đầu và giờ kết thúc phải khác nhau.");
        }
    }
}
