using System.Text.RegularExpressions;
using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.GoogleRegister;

public sealed class GoogleRegisterValidator : AbstractValidator<GoogleRegisterCommand>
{
    private static readonly Regex UsernamePattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._]*[A-Za-z0-9]$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(
        @"^(?:0|\+84)[35789][0-9]{8}$", RegexOptions.Compiled);

    public GoogleRegisterValidator()
    {
        RuleFor(x => x.Credential).NotEmpty().MaximumLength(8192);
        RuleFor(x => x.Username).Cascade(CascadeMode.Stop)
            .NotEmpty().Must(value => value.Trim().Length is >= 3 and <= 100)
            .Must(value => UsernamePattern.IsMatch(value.Trim()));
        RuleFor(x => x.Phone).Cascade(CascadeMode.Stop)
            .NotEmpty().Must(value => PhonePattern.IsMatch(value.Trim()));
        RuleFor(x => x.FullName).Cascade(CascadeMode.Stop)
            .NotEmpty().Must(value => value.Trim().Length is >= 1 and <= 255);
    }
}
