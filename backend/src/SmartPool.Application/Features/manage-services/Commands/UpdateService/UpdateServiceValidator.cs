using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.UpdateService
{
    public sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceCommand>
    {
        public UpdateServiceValidator()
        {
            RuleFor(command => command.Id).NotEmpty().WithMessage("Mã dịch vụ không hợp lệ.");
            RuleFor(command => command.Name).NotEmpty().WithMessage("Tên dịch vụ không được để trống.")
                .MaximumLength(255).WithMessage("Tên dịch vụ không được vượt quá 255 ký tự.");
            RuleFor(command => command.Type).Must(type => type is "Sale" or "Rental")
                .WithMessage("Loại dịch vụ phải là Sale hoặc Rental.");
            RuleFor(command => command.Price).GreaterThanOrEqualTo(0).WithMessage("Giá dịch vụ không được âm.");
        }
    }
}
