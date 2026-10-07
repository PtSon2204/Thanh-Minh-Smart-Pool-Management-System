using FluentValidation;

namespace SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;

public sealed class SetServiceStatusValidator : AbstractValidator<SetServiceStatusCommand>
{
    public SetServiceStatusValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("Mã dịch vụ không hợp lệ.");
    }
}
