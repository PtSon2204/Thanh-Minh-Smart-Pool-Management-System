using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShift
{
    public sealed class AssignShiftValidator : AbstractValidator<AssignShiftCommand>
    {
        public AssignShiftValidator()
        {
            RuleFor(item => item.EmployeeId).NotEmpty().WithMessage("Mã nhân viên không được để trống.");
            RuleFor(item => item.ShiftId).NotEmpty().WithMessage("Mã ca làm việc không được để trống.");
        }
    }
}
