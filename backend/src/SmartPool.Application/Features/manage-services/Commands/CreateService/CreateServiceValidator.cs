using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.CreateService
{
    public sealed class CreateServiceValidator : AbstractValidator<CreateServiceCommand>
    {
        public CreateServiceValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Tên dịch vụ không được để trống.")
                .MaximumLength(255).WithMessage("Tên dịch vụ không được vượt quá 255 ký tự.");
            RuleFor(command => command.Type).Must(type => type is "Sale" or "Rental")
                .WithMessage("Loại dịch vụ phải là Sale hoặc Rental.");
            RuleFor(command => command.Price).GreaterThanOrEqualTo(0).WithMessage("Giá dịch vụ không được âm.");
            RuleFor(command => command.StockQuantity).Must(quantity => quantity is null or 0)
                .WithMessage("Sản phẩm mới bắt đầu với tồn kho 0. Hãy lập phiếu nhập để ghi nhận số lượng và giá nhập.");
        }
    }
}
