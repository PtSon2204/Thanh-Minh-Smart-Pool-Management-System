using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassAttendance;

public sealed class ReviewClassAttendanceValidator : AbstractValidator<ReviewClassAttendanceCommand>
{
    public ReviewClassAttendanceValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.AttendanceId).NotEmpty();
        RuleFor(command => command.Decision)
            .Must(decision => string.Equals(decision?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(decision?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Kết quả phải là Verified hoặc Rejected.");
        RuleFor(command => command.ReviewNote)
            .Must(note => note is null || note.Trim().Length <= 500)
            .WithMessage("Ghi chú duyệt tối đa 500 ký tự.");
        When(command => string.Equals(command.Decision?.Trim(), "Rejected",
                StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(command => command.ReviewNote)
                .Must(note => !string.IsNullOrWhiteSpace(note))
                .WithMessage("Từ chối điểm danh phải ghi lý do."));
        RuleFor(command => command.ExpectedUpdatedAt)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Cần thời gian cập nhật gần nhất của điểm danh.");
    }
}
