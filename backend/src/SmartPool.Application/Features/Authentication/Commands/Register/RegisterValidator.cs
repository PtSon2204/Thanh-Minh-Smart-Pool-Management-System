using FluentValidation;
using System.Text.RegularExpressions;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex EmailPattern = new(
        @"^[A-Za-z0-9](?:[A-Za-z0-9._%+\-]*[A-Za-z0-9])?@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+$",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"^(?:0|\+84)[35789][0-9]{8}$",
        RegexOptions.Compiled);

    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Email không được để trống.")
            .Must(value => value.Trim().Length <= 150)
                .WithMessage("Email không được vượt quá 150 ký tự.")
            .Must(value => EmailPattern.IsMatch(value.Trim()) && !value.Trim().Contains(".."))
                .WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Số điện thoại không được để trống.")
            .Must(value => PhonePattern.IsMatch(value.Trim()))
                .WithMessage("Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .Must(value => value.Length is >= 8 and <= 128)
                .WithMessage("Mật khẩu phải dài từ 8 đến 128 ký tự.")
            .Must(value => value.Any(char.IsLetter) && value.Any(char.IsDigit))
                .WithMessage("Mật khẩu phải có ít nhất một chữ cái và một chữ số.");
    }
}
