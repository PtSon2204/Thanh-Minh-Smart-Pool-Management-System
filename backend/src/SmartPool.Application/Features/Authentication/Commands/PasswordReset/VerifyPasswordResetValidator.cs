using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class VerifyPasswordResetValidator : AbstractValidator<VerifyPasswordResetCommand>
{
    public VerifyPasswordResetValidator()
    {
        RuleFor(command => command.RecoveryId)
            .NotEmpty().WithMessage("Mã phiên khôi phục không hợp lệ.");
        RuleFor(command => command.Otp)
            .Must(otp => otp is { Length: 6 } && otp.All(char.IsAsciiDigit))
            .WithMessage("OTP phải gồm đúng 6 chữ số.");
    }
}
