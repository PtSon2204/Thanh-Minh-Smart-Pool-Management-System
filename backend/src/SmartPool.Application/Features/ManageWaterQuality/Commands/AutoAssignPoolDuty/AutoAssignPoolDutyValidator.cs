using FluentValidation;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.AutoAssignPoolDuty;

public sealed class AutoAssignPoolDutyValidator : AbstractValidator<AutoAssignPoolDutyCommand>
{
    public AutoAssignPoolDutyValidator()
    {
        RuleFor(x => x.FromDate)
            .NotEmpty()
            .WithMessage("Vui lòng chọn ngày bắt đầu phân công lịch trực bể.");

        RuleFor(x => x.ToDate)
            .NotEmpty()
            .WithMessage("Vui lòng chọn ngày kết thúc phân công lịch trực bể.")
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithMessage("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 31)
            .WithMessage("Khoảng thời gian tự động gán lịch trực bể tối đa là 31 ngày mỗi lần.");

        RuleFor(x => x.DefaultNotes)
            .MaximumLength(500)
            .WithMessage("Ghi chú mặc định không được vượt quá 500 ký tự.");
    }
}

