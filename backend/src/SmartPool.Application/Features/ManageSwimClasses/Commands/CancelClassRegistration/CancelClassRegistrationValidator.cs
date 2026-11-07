using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassRegistration;

public sealed class CancelClassRegistrationValidator : AbstractValidator<CancelClassRegistrationCommand>
{
    public CancelClassRegistrationValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.RegistrationId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty().Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(500).WithMessage("Lý do hủy đăng ký phải có từ 1 đến 500 ký tự.");
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Cần thời gian cập nhật gần nhất của đăng ký.");
    }
}
