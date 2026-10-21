using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalValidator : AbstractValidator<CheckoutRentalCommand>
    {
        public CheckoutRentalValidator()
        {
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
            RuleFor(command => command.ProductId).NotEmpty().WithMessage("Mã sản phẩm không hợp lệ.");
            RuleFor(command => command.Quantity).GreaterThan(0).WithMessage("Số lượng thuê phải lớn hơn 0.");
            RuleFor(command => command.DepositPerUnit).GreaterThanOrEqualTo(0).WithMessage("Tiền cọc không được âm.");
            RuleFor(command => command.CustomerName).MaximumLength(255).When(command => command.CustomerName is not null)
                .WithMessage("Tên khách hàng không được vượt quá 255 ký tự.");
            RuleFor(command => command.CustomerPhone).MaximumLength(20).When(command => command.CustomerPhone is not null)
                .WithMessage("Số điện thoại không được vượt quá 20 ký tự.");
        }
    }
}
