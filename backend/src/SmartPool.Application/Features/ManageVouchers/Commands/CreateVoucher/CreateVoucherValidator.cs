using FluentValidation;

namespace SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher
{
    public sealed class CreateVoucherValidator : AbstractValidator<CreateVoucherCommand>
    {
        public CreateVoucherValidator()
        {
            RuleFor(item => item.Code)
                .NotEmpty().WithMessage("Mã voucher không được để trống.");
                
            RuleFor(item => item.DiscountType)
                .Must(value => value == "PERCENTAGE" || value == "FIXED_AMOUNT")
                .WithMessage("Loại giảm giá không hợp lệ. Chỉ chấp nhận PERCENTAGE hoặc FIXED_AMOUNT.");
                
            RuleFor(item => item.DiscountValue)
                .GreaterThan(0).WithMessage("Giá trị giảm giá phải lớn hơn 0.");
                
            RuleFor(item => item.MinOrderValue)
                .GreaterThanOrEqualTo(0).When(item => item.MinOrderValue.HasValue)
                .WithMessage("Giá trị đơn hàng tối thiểu không được âm.");
                
            RuleFor(item => item.EndDate)
                .GreaterThan(item => item.StartDate).When(item => item.StartDate.HasValue && item.EndDate.HasValue)
                .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
        }
    }
}
