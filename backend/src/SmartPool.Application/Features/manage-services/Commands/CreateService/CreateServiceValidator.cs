using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.CreateService
{
    public sealed class CreateServiceValidator : AbstractValidator<CreateServiceCommand>
    {
        public CreateServiceValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Tên dịch vụ không được để trống.")
                .MaximumLength(255).WithMessage("Tên dịch vụ không được vượt quá 255 ký tự.");
            RuleFor(command => command.Price).GreaterThanOrEqualTo(0).WithMessage("Giá dịch vụ không được âm.");
            RuleFor(command => command.StockQuantity).NotNull().GreaterThanOrEqualTo(0)
                .WithMessage("Số lượng đồ cho thuê phải là số nguyên không âm.");
        }
    }
}
