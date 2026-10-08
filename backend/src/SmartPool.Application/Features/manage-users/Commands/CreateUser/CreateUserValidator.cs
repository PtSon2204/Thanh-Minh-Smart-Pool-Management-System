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
            .WithMessage("Vai trò phải là CUSTOMER, STAFF hoặc ADMIN.");

        RuleFor(command => command).CustomAsync(async (command, context, cancellationToken) =>
        {
            var registration = new RegisterCommand
            {
                Username = command.Username,
                Email = command.Email,
                Phone = command.Phone,
                Password = command.Password,
                ConfirmPassword = command.ConfirmPassword,
                FullName = command.FullName,
                DateOfBirth = command.DateOfBirth,
                Address = command.Address
            };
            var result = await new RegisterValidator().ValidateAsync(registration, cancellationToken);
            foreach (var error in result.Errors)
                context.AddFailure(error);
        });
    }
}
