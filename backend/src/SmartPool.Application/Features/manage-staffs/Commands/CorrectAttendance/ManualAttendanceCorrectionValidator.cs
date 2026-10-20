using FluentValidation;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;

public sealed class ManualAttendanceCorrectionValidator : AbstractValidator<ManualAttendanceCorrectionCommand>
{
    public ManualAttendanceCorrectionValidator()
    {
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.PerformedByUserId).NotEmpty();
        RuleFor(command => command.ExpectedUpdatedAtUtc).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.UpdateCheckIn || command.UpdateCheckOut).Equal(true)
            .WithMessage("Chọn ít nhất một giờ cần điều chỉnh.");
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(500);

        When(command => command.UpdateCheckIn, () =>
        {
            RuleFor(command => command.CheckInTime).NotNull();
            RuleFor(command => command.CheckInTime).Must(ManualAttendanceCorrectionPolicy.HasVietnamOffset)
                .When(command => command.CheckInTime is not null)
                .WithMessage("Giờ vào phải có múi giờ Việt Nam (+07:00).");
        });
        When(command => !command.UpdateCheckIn, () => RuleFor(command => command.CheckInTime).Null());

        When(command => command.UpdateCheckOut, () =>
        {
            RuleFor(command => command.CheckOutTime).NotNull();
            RuleFor(command => command.CheckOutTime).Must(ManualAttendanceCorrectionPolicy.HasVietnamOffset)
                .When(command => command.CheckOutTime is not null)
                .WithMessage("Giờ ra phải có múi giờ Việt Nam (+07:00).");
        });
        When(command => !command.UpdateCheckOut, () => RuleFor(command => command.CheckOutTime).Null());

        When(command => command.UpdateCheckIn && command.UpdateCheckOut &&
            command.CheckInTime is not null && command.CheckOutTime is not null, () =>
            RuleFor(command => command.CheckOutTime).Must((command, checkOut) => checkOut > command.CheckInTime)
                .WithMessage("Giờ ra phải sau giờ vào."));
    }
}
