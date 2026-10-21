using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class ResendRegistrationValidator : AbstractValidator<ResendRegistrationCommand>
{
    public ResendRegistrationValidator()
    {
        RuleFor(command => command.RegistrationId)
            .NotEmpty().WithMessage("Mã phiên đăng ký không hợp lệ.");
    }
}
