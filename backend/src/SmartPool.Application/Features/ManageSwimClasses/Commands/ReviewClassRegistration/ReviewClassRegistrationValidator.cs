using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

public sealed class ReviewClassRegistrationValidator : AbstractValidator<ReviewClassRegistrationCommand>
{
    public ReviewClassRegistrationValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.RegistrationId).NotEmpty();
        RuleFor(command => command.Decision)
            .Must(decision => string.Equals(decision?.Trim(), "Accepted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(decision?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là Accepted hoặc Rejected.");
        RuleFor(command => command.ReviewNote)
            .Must(note => note is null || note.Trim().Length <= 500)
            .WithMessage("Ghi chú duyệt tối đa 500 ký tự.");
        When(command => string.Equals(command.Decision?.Trim(), "Rejected",
                StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(command => command.ReviewNote)
                .Must(note => !string.IsNullOrWhiteSpace(note))
                .WithMessage("Từ chối đăng ký phải ghi lý do."));
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Cần thời gian cập nhật gần nhất của đăng ký.");
    }
}
