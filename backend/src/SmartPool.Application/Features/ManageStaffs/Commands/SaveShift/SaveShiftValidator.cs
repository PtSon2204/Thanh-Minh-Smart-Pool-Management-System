using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveShift
{
    public sealed class SaveShiftValidator : AbstractValidator<SaveShiftCommand>
    {
        public SaveShiftValidator()
        {
            RuleFor(item => item.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100)
                .WithMessage("Tên ca làm việc không hợp lệ.");
        }
    }
}
