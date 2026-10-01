using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalValidator : AbstractValidator<ReturnRentalCommand>
    {
        public ReturnRentalValidator()
        {
            RuleFor(command => command.RentalId).NotEmpty().WithMessage("Mã lượt thuê không hợp lệ.");
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
        }
    }
}
