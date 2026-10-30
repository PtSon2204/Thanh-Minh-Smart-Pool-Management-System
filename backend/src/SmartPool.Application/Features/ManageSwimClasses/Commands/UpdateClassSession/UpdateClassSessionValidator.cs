using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;

public sealed class UpdateClassSessionValidator : AbstractValidator<UpdateClassSessionCommand>
{
    public UpdateClassSessionValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Cần thời gian cập nhật gần nhất của buổi học.");
        RuleFor(command => command.StartsAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Giờ bắt đầu không hợp lệ.")
            .Must(startsAt => startsAt.UtcDateTime > timeProvider.GetUtcNow().UtcDateTime)
            .WithMessage("Buổi học phải bắt đầu trong tương lai.");
        RuleFor(command => command.EndsAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("Giờ kết thúc không hợp lệ.")
            .GreaterThan(command => command.StartsAt)
            .WithMessage("Giờ kết thúc phải sau giờ bắt đầu.")
            .Must((command, endsAt) =>
                command.StartsAt.ToOffset(TimeSpan.FromHours(7)).Date ==
                endsAt.ToOffset(TimeSpan.FromHours(7)).Date)
            .WithMessage("Giờ kết thúc phải cùng ngày với giờ bắt đầu theo giờ Việt Nam.");
    }
}
