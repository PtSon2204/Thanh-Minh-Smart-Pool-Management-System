using FluentValidation;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusValidator()
    {
        RuleFor(request => request.Id).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(request => request.Status)
            .Must(status => string.Equals(status?.Trim(), nameof(UserStatusEnum.ACTIVE), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status?.Trim(), nameof(UserStatusEnum.LOCKED), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Trạng thái phải là ACTIVE hoặc LOCKED.");
    }
}
