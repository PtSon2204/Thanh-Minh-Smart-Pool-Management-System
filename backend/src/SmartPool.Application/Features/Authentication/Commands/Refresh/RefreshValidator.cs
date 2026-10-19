using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Refresh;

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Vui lòng cung cấp refresh token.")
            .MaximumLength(512).WithMessage("Refresh token không hợp lệ.");
    }
}
