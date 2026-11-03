using FluentValidation;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.UpdatePoolDutySchedule;

public sealed class UpdatePoolDutyScheduleValidator : AbstractValidator<UpdatePoolDutyScheduleCommand>
{
    private static readonly string[] AllowedStatuses =
        ["Scheduled", "InProgress", "Completed", "Absent", "Cancelled"];

    public UpdatePoolDutyScheduleValidator()
    {
        RuleFor(x => x.DutyDate)
            .NotEmpty()
            .WithMessage("Vui lòng chọn ngày trực bể.");

        RuleFor(x => x.ShiftId)
            .NotEmpty()
            .WithMessage("Vui lòng chọn ca trực bể.");

        RuleFor(x => x.AssignedEmployeeId)
            .NotEmpty()
            .WithMessage("Vui lòng chọn nhân viên trực bể.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => AllowedStatuses.Contains(s, StringComparer.Ordinal))
            .WithMessage("Trạng thái lịch trực không hợp lệ. Chỉ chấp nhận: Scheduled, InProgress, Completed, Absent, Cancelled.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

