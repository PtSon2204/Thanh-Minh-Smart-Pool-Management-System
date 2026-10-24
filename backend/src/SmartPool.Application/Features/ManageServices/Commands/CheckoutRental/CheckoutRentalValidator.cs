using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.CheckoutRental
{
    public sealed class CheckoutRentalValidator : AbstractValidator<CheckoutRentalCommand>
    {
        public CheckoutRentalValidator()
        {
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
            RuleFor(command => command.RequestId).NotEmpty();
            RuleFor(command => command.CollectionConfirmed).Equal(true).WithMessage("Xác nhận đã thu đủ phí thuê và tiền cọc.");
            RuleFor(command => command.CustomerName).NotEmpty().MaximumLength(255).WithMessage("Nhập tên khách hàng hợp lệ.");
            RuleFor(command => command.CustomerPhone).NotEmpty().MaximumLength(20).WithMessage("Nhập số điện thoại khách hàng.");
            RuleFor(command => command.Items).NotNull().NotEmpty();
            RuleFor(command => command.Items).Must(items => items is null || (items.Count <= 20 && items.All(item => item is not null) && items.Sum(item => (long)item.Quantity) <= 100 && items.Select(item => item.ProductId).Distinct().Count() == items.Count))
                .WithMessage("Chọn các sản phẩm khác nhau, tối đa 100 đơn vị mỗi đơn.");
            When(command => command.Items is not null && command.Items.All(item => item is not null), () =>
                RuleForEach(command => command.Items).ChildRules(line =>
                {
                    line.RuleFor(item => item.ProductId).NotEmpty();
                    line.RuleFor(item => item.Quantity).InclusiveBetween(1, 100);
                    line.RuleFor(item => item.ExpectedUnitPrice).GreaterThanOrEqualTo(0);
                }));
        }
    }
}
