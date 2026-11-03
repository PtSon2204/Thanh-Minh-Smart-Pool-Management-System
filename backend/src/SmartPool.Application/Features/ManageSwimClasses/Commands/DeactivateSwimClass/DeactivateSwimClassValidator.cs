using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

public sealed class DeactivateSwimClassValidator : AbstractValidator<DeactivateSwimClassCommand>
{
    public DeactivateSwimClassValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(500).WithMessage("Lý do ngừng lớp phải có từ 1 đến 500 ký tự.");
        RuleFor(command => command.ExpectedUpdatedAt).NotEqual(default(DateTimeOffset));
    }
}
