using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;

public sealed class CancelClassSessionValidator : AbstractValidator<CancelClassSessionCommand>
{
    public CancelClassSessionValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty().Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(500).WithMessage("Lý do hủy buổi phải có từ 1 đến 500 ký tự.");
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Cần thời gian cập nhật gần nhất của buổi học.");
    }
}
