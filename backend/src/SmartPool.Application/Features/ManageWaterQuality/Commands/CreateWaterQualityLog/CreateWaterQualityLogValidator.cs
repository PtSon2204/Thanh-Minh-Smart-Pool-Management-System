using FluentValidation;

namespace SmartPool.Application.Features.ManageWaterQuality.Commands.CreateWaterQualityLog;

public sealed class CreateWaterQualityLogValidator : AbstractValidator<CreateWaterQualityLogCommand>
{
    public CreateWaterQualityLogValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PhValue)
            .InclusiveBetween(0m, 14m)
            .WithMessage("Chỉ số pH phải nằm trong khoảng từ 0.00 đến 14.00.");

        RuleFor(x => x.FreeChlorineMgL)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Chỉ số Clo tự do không được âm.")
            .LessThanOrEqualTo(20m)
            .WithMessage("Chỉ số Clo tự do vượt quá ngưỡng tối đa cho phép đo (20 mg/L).");

        RuleFor(x => x.InspectionTime)
            .Must(time => time <= timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5))
            .WithMessage("Thời gian đo không được vượt quá thời điểm hiện tại.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
