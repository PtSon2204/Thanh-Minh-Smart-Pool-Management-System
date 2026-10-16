using System.Text.RegularExpressions;
using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.PasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .Must(email => !string.IsNullOrWhiteSpace(email))
                .WithMessage("Email không được để trống.")
            .Must(email => email.Trim().Length <= 150)
                .WithMessage("Email không được vượt quá 150 ký tự.")
            .Must(email => IsValid(email.Trim()))
                .WithMessage("Email không đúng định dạng.");
    }

    private readonly Regex Pattern = new(
        @"^[A-Za-z0-9](?:[A-Za-z0-9._%+\-]*[A-Za-z0-9])?@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+$",
        RegexOptions.Compiled);

    private bool IsValid(string value) => Pattern.IsMatch(value) && !value.Contains("..");
}
