using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff
{
    public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
    {
        public UpdateStaffValidator()
        {
            RuleFor(item => item.UserId).NotEmpty().WithMessage("Mã nhân viên không được để trống.");
            RuleFor(item => item.BaseSalary).GreaterThanOrEqualTo(0).WithMessage("Lương cơ bản không được âm.");
            RuleFor(item => item.Status).Must(value => value is "Working" or "Inactive").WithMessage("Trạng thái nhân viên không hợp lệ.");
        }
    }
}
