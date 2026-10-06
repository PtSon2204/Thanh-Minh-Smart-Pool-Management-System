using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff
{
    public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
    {
        public CreateStaffValidator()
        {
            RuleFor(item => item.UserId).NotEmpty().WithMessage("Mã tài khoản không được để trống.");
            RuleFor(item => item.JoinDate).NotEqual(default(DateOnly)).WithMessage("Ngày vào làm không hợp lệ.");
            RuleFor(item => item.BaseSalary).GreaterThanOrEqualTo(0).WithMessage("Lương cơ bản không được âm.");
            RuleFor(item => item.Status).Must(value => value is "Working" or "Inactive").WithMessage("Trạng thái nhân viên không hợp lệ.");
        }
    }
}
