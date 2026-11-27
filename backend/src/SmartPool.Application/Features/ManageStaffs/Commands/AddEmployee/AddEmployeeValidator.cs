using System.Net.Mail;
using System.Text.RegularExpressions;
using FluentValidation;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;

public sealed class AddEmployeeValidator : AbstractValidator<AddEmployeeCommand>
{
    private readonly TimeProvider clock;

    public AddEmployeeValidator(TimeProvider clock)
    {
        this.clock = clock;
        RuleFor(item => item.FullName).NotEmpty().WithMessage("Nhập họ và tên.")
            .MaximumLength(255).WithMessage("Họ và tên tối đa 255 ký tự.");
        RuleFor(item => item.Gender).Must(value => value is "MALE" or "FEMALE").WithMessage("Chọn giới tính.");
        RuleFor(item => item.DateOfBirth).NotEqual(default(DateOnly)).WithMessage("Nhập ngày sinh.")
            .Must(value => StaffOnboardingValidation.IsAtLeastAge(value, this.clock))
            .WithMessage(StaffOnboardingValidation.MinimumAgeMessage);
        RuleFor(item => item.Phone).NotEmpty().WithMessage("Nhập số điện thoại.")
            .Must(value => Regex.IsMatch(StaffOnboardingValidation.Phone(value), VietnameseMobilePhone.ValidationPattern))
            .WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(item => item.PersonalEmail).NotEmpty().WithMessage("Nhập email cá nhân.")
            .MaximumLength(150).WithMessage("Email cá nhân tối đa 150 ký tự.")
            .Must(value => MailAddress.TryCreate(value, out var parsed) && parsed.Address == value?.Trim() &&
                !value.Trim().EndsWith("@smartpool.com", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Email cá nhân không hợp lệ.");
        RuleFor(item => item.CccdNumber).Matches("^[0-9]{12}$").WithMessage("Số CCCD phải gồm 12 chữ số.");
        RuleFor(item => item.PermanentAddress).NotEmpty().WithMessage("Nhập địa chỉ thường trú.")
            .MaximumLength(500).WithMessage("Địa chỉ thường trú tối đa 500 ký tự.");
        RuleFor(item => item.TemporaryAddress).MaximumLength(500).WithMessage("Địa chỉ tạm trú tối đa 500 ký tự.");
        RuleFor(item => item.JoinDate).NotEqual(default(DateOnly)).WithMessage("Nhập ngày vào làm.")
            .Must(value => value >= VietnamTimeBoundary.GetDate(this.clock))
            .WithMessage("Ngày vào làm phải từ hôm nay (Việt Nam) trở đi.");
        RuleFor(item => item.TargetRole).Must(value => value is "LIFEGUARD" or "SALES")
            .WithMessage("Vai trò phải là LIFEGUARD hoặc SALES.");
    }
}
