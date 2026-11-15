using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

public sealed class SubmitClassAttendanceValidator : AbstractValidator<SubmitClassAttendanceCommand>
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    public SubmitClassAttendanceValidator()
    {
        RuleFor(command => command.ClassId).NotEmpty();
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Image)
            .NotNull().WithMessage("Vui lòng chọn ảnh điểm danh.")
            .Must(file => file is not null && file.Length is >= 4 and <= MaxImageBytes &&
                          file.ContentType is "image/jpeg" or "image/png")
            .WithMessage("Chỉ nhận ảnh JPG/PNG tối đa 5 MB.");
    }
}
