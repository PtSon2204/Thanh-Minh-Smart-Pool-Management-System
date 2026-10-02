using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.")
            .Must(value => value.Trim().Length <= 150)
                .WithMessage("Thông tin đăng nhập không được vượt quá 150 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}
