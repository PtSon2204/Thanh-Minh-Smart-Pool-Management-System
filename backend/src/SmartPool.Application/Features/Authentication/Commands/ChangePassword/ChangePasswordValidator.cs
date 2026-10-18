using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.ChangePassword;

public sealed class ChangePasswordValidator
    : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Vui lòng nhập mật khẩu mới.")
            .Must(value => value.Length is >= 8 and <= 128)
                .WithMessage("Mật khẩu phải dài từ 8 đến 128 ký tự.")
            .Must(value => value.Any(char.IsLetter) && value.Any(char.IsDigit))
                .WithMessage("Mật khẩu phải có ít nhất một chữ cái và một chữ số.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Mật khẩu xác nhận không khớp.");

        RuleFor(x => x.NewPassword)
            .Must((command, newPassword) =>
                newPassword != command.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}