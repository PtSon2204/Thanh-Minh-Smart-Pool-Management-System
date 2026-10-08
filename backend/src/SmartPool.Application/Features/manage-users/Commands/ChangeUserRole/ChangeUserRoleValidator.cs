using FluentValidation;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;

public sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(request => request.Id)
            .NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(request => request.Role)
            .Must(role => !string.IsNullOrWhiteSpace(role) &&
                Enum.GetNames<RoleEnum>().Any(name =>
                    string.Equals(name, role.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Vai trò phải là CUSTOMER, STAFF hoặc ADMIN.");
    }
}
