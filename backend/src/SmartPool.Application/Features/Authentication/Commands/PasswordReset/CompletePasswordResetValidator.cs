using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class CompletePasswordResetValidator : AbstractValidator<CompletePasswordResetCommand>
{
    public CompletePasswordResetValidator()
    {
        RuleFor(command => command.RecoveryId)
            .NotEmpty().WithMessage("Mã phiên khôi phục không hợp lệ.");
        RuleFor(command => command.ResetToken)
            .Must(token => token is { Length: 64 } && token.All(char.IsAsciiHexDigit))
            .WithMessage("Reset token không hợp lệ.");
        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .Must(HasValidLength)
                .WithMessage("Mật khẩu phải dài từ 8 đến 128 ký tự.")
            .Must(HasLetterAndDigit)
                .WithMessage("Mật khẩu phải có ít nhất một chữ cái và một chữ số.");
        RuleFor(command => command.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng xác nhận mật khẩu.")
            .Equal(command => command.NewPassword).WithMessage("Mật khẩu xác nhận không khớp.");
    }

    private bool HasValidLength(string value) => value.Length is >= 8 and <= 128;
    private bool HasLetterAndDigit(string value) => value.Any(char.IsLetter) && value.Any(char.IsDigit);
}
