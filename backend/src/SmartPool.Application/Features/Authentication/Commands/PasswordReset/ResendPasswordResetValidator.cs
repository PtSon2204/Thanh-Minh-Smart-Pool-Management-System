using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class ResendPasswordResetValidator : AbstractValidator<ResendPasswordResetCommand>
{
    public ResendPasswordResetValidator()
    {
        RuleFor(command => command.RecoveryId)
            .NotEmpty().WithMessage("Mã phiên khôi phục không hợp lệ.");
    }
}
