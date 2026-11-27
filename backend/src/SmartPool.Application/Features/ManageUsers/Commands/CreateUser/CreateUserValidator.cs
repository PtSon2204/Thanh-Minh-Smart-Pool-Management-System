using FluentValidation;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Commands.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.Role)
            .Must(role => !string.IsNullOrWhiteSpace(role) &&
                Enum.GetNames<RoleEnum>().Any(name =>
                    string.Equals(name, role.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Vai trò phải là CUSTOMER, LIFEGUARD, SALES, COACH hoặc ADMIN.");

        RuleFor(command => command.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MinimumLength(3).WithMessage("Tên đăng nhập phải dài từ 3 đến 100 ký tự.")
            .Matches("^[A-Za-z0-9](?:[A-Za-z0-9._]*[A-Za-z0-9])?$")
                .WithMessage("Tên đăng nhập không hợp lệ.")
            .MaximumLength(100).WithMessage("Tên đăng nhập không được vượt quá 100 ký tự.");
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ tên không được vượt quá 255 ký tự.");
        RuleFor(command => command.DateOfBirth)
            .Must(value => !value.HasValue || value.Value <= DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Ho_Chi_Minh")))
            .WithMessage("Ngày sinh không được ở tương lai.");
        RuleFor(command => command.Address)
            .Must(value => value is null || value.Trim().Length <= 500)
            .WithMessage("Địa chỉ không được vượt quá 500 ký tự.");

        RuleFor(command => command).CustomAsync(async (command, context, cancellationToken) =>
        {
            var registration = new RegisterCommand
            {
                Email = command.Email,
                Phone = command.Phone
            };
            var result = await new RegisterValidator().ValidateAsync(
                registration, options => options.IncludeProperties("Email", "Phone"), cancellationToken);
            foreach (var error in result.Errors)
                context.AddFailure(error);
        });
    }
}
