using FluentValidation;
using System.Text.RegularExpressions;

namespace SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    private static readonly Regex EmailPattern = new(
        @"^[A-Za-z0-9](?:[A-Za-z0-9._%+\-]*[A-Za-z0-9])?@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+$",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"^(?:0|\+84)[35789][0-9]{8}$",
        RegexOptions.Compiled);

    public UpdateUserValidator()
    {
        RuleFor(request => request.Id)
            .NotEmpty().WithMessage("Mã người dùng không hợp lệ.");

        RuleFor(request => request.Email)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Email không được để trống.")
            .Must(value => value.Trim().Length <= 150).WithMessage("Email không được vượt quá 150 ký tự.")
            .Must(value => EmailPattern.IsMatch(value.Trim()) && !value.Trim().Contains(".."))
            .WithMessage("Email không đúng định dạng.");

        RuleFor(request => request.Phone)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Số điện thoại không được để trống.")
            .Must(value => PhonePattern.IsMatch(value.Trim()))
            .WithMessage("Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.");

        RuleFor(request => request.FullName)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Họ tên không được để trống.")
            .Must(value => value.Trim().Length <= 255).WithMessage("Họ tên không được vượt quá 255 ký tự.");

        RuleFor(request => request.DateOfBirth)
            .Must(value => !value.HasValue || value.Value <= DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Ho_Chi_Minh")))
            .WithMessage("Ngày sinh không được ở tương lai.");

        RuleFor(request => request.Address)
            .Must(value => value is null || value.Trim().Length <= 500)
            .WithMessage("Địa chỉ không được vượt quá 500 ký tự.");
    }
}
