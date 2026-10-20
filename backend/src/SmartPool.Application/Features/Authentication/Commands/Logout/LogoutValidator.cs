using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Logout;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Vui lòng cung cấp refresh token.")
            .MaximumLength(512).WithMessage("Refresh token không hợp lệ.");
    }
}
