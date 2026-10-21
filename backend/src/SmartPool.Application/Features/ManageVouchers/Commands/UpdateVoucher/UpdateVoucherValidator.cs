using FluentValidation;

namespace SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher
{
    public sealed class UpdateVoucherValidator : AbstractValidator<UpdateVoucherCommand>
    {
        public UpdateVoucherValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Mã voucher không được để trống.");

            RuleFor(x => x.DiscountType)
                .Must(v => v == "PERCENTAGE" || v == "FIXED_AMOUNT")
                .WithMessage("Loại giảm giá không hợp lệ. Chỉ chấp nhận PERCENTAGE hoặc FIXED_AMOUNT.");

            RuleFor(x => x.DiscountValue)
                .GreaterThan(0).WithMessage("Giá trị giảm phải lớn hơn 0.");

            RuleFor(x => x.MinOrderValue)
                .GreaterThanOrEqualTo(0).When(x => x.MinOrderValue.HasValue)
                .WithMessage("Giá trị đơn hàng tối thiểu không được âm.");

            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate)
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
        }
    }
}
