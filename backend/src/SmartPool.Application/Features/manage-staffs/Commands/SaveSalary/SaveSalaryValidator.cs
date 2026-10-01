using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary
{
    public sealed class SaveSalaryValidator : AbstractValidator<SaveSalaryCommand>
    {
        public SaveSalaryValidator()
        {
            RuleFor(item => item.UserId).NotEmpty().WithMessage("Mã nhân viên không được để trống.");
            RuleFor(item => item.Month).InclusiveBetween(1, 12).WithMessage("Tháng phải từ 1 đến 12.");
            RuleFor(item => item.Year).InclusiveBetween(1, 9999).WithMessage("Năm không hợp lệ.");
            RuleFor(item => item.TotalShifts).GreaterThanOrEqualTo(0).WithMessage("Số ca không được âm.");
            RuleFor(item => item.Bonus).GreaterThanOrEqualTo(0).WithMessage("Thưởng không được âm.");
            RuleFor(item => item.Deduction).GreaterThanOrEqualTo(0).WithMessage("Khấu trừ không được âm.");
            RuleFor(item => item.NetSalary).GreaterThanOrEqualTo(0).WithMessage("Lương thực nhận không được âm.");
        }
    }
}
