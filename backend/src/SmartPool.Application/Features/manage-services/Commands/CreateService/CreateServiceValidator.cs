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
            RuleFor(command => command.StockQuantity).GreaterThanOrEqualTo(0)
                .When(command => command.StockQuantity.HasValue)
                .WithMessage("Số lượng tồn kho không được âm.");
        }
    }
}
