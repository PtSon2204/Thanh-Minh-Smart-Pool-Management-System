using FluentValidation;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Constants;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed class CreateClassSessionsValidator : AbstractValidator<CreateClassSessionsCommand>
{
    public CreateClassSessionsValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.Sessions)
            .NotEmpty().WithMessage("Cần ít nhất một buổi học.");

        RuleForEach(command => command.Sessions).ChildRules(session =>
        {
            session.RuleFor(item => item.StartsAt)
                .NotEqual(default(DateTimeOffset)).WithMessage("Giờ bắt đầu không hợp lệ.")
                .Must(startsAt => startsAt.UtcDateTime > timeProvider.GetUtcNow().UtcDateTime)
                .WithMessage("Buổi học phải bắt đầu trong tương lai.");
            session.RuleFor(item => item.EndsAt)
                .NotEqual(default(DateTimeOffset)).WithMessage("Giờ kết thúc không hợp lệ.")
                .GreaterThan(item => item.StartsAt)
                .WithMessage("Giờ kết thúc phải sau giờ bắt đầu.")
                .Must((item, endsAt) =>
                    item.StartsAt.ToOffset(TimeSpan.FromHours(7)).Date ==
                    endsAt.ToOffset(TimeSpan.FromHours(7)).Date)
                .WithMessage("Giờ kết thúc phải cùng ngày với giờ bắt đầu theo giờ Việt Nam.")
                .Must((item, endsAt) => FixedShifts.ContainsClassSession(item.StartsAt, endsAt))
                .WithMessage("Buổi học phải nằm trọn trong ca sáng 06:00–11:30 hoặc ca chiều 13:30–19:30 (giờ Việt Nam).");
        });
    }
}
