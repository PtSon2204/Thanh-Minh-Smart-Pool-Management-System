using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class VerifyRegistrationValidator : AbstractValidator<VerifyRegistrationCommand>
{
    public VerifyRegistrationValidator()
    {
        RuleFor(command => command.RegistrationId)
            .NotEmpty().WithMessage("Mã phiên đăng ký không hợp lệ.");
        RuleFor(command => command.Otp)
            .Matches("^[0-9]{6}$").WithMessage("OTP phải gồm đúng 6 chữ số.");
    }
}
