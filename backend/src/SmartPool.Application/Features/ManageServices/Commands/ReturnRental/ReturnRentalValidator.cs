using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.ReturnRental
{
    public sealed class ReturnRentalValidator : AbstractValidator<ReturnRentalCommand>
    {
        public ReturnRentalValidator()
        {
            RuleFor(command => command.RentalId).NotEmpty().WithMessage("Mã lượt thuê không hợp lệ.");
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
            RuleFor(command => command.RequestId).NotEmpty();
            RuleFor(command => command.SettlementConfirmed).Equal(true).WithMessage("Xác nhận quyết toán cọc.");
            RuleFor(command => command.Condition).Must(condition => condition is "Normal" or "Damaged" or "Lost");
            RuleFor(command => command.Reason).MaximumLength(500);
            When(command => command.Condition is "Damaged" or "Lost", () => RuleFor(command => command.Reason).NotEmpty());
        }
    }
}
