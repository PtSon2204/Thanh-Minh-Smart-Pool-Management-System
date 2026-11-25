using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Services;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.PreviewWaterQualityEvaluation;

public sealed class PreviewWaterQualityEvaluationQuery : IRequest<WaterQualityEvaluationResult>
{
    public decimal PhValue { get; set; }
    public decimal FreeChlorineMgL { get; set; }
    public bool? IsWaterClear { get; set; }
}

public sealed class PreviewWaterQualityEvaluationValidator : AbstractValidator<PreviewWaterQualityEvaluationQuery>
{
    public PreviewWaterQualityEvaluationValidator()
    {
        RuleFor(x => x.PhValue)
            .InclusiveBetween(0m, 14m)
            .WithMessage("Chỉ số pH phải nằm trong khoảng từ 0.00 đến 14.00.");

        RuleFor(x => x.FreeChlorineMgL)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Chỉ số Clo tự do không được âm.")
            .LessThanOrEqualTo(20m)
            .WithMessage("Chỉ số Clo tự do vượt quá ngưỡng tối đa cho phép đo (20 mg/L).");
    }
}

public sealed class PreviewWaterQualityEvaluationHandler : IRequestHandler<PreviewWaterQualityEvaluationQuery, WaterQualityEvaluationResult>
{
    private readonly IWaterQualityEvaluator _evaluator;
    private readonly IValidator<PreviewWaterQualityEvaluationQuery> _validator;
    private readonly IWaterQualityOperations? _operations;

    public PreviewWaterQualityEvaluationHandler(
        IWaterQualityEvaluator evaluator,
        IValidator<PreviewWaterQualityEvaluationQuery> validator,
        IWaterQualityOperations? operations = null)
    {
        _evaluator = evaluator;
        _validator = validator;
        _operations = operations;
    }

    public async Task<WaterQualityEvaluationResult> Handle(PreviewWaterQualityEvaluationQuery request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = _evaluator.Evaluate(request.PhValue, request.FreeChlorineMgL, request.IsWaterClear);

        if (_operations is not null)
        {
            var trend = await _operations.EvaluateThreeDayTrendPreviewAsync(
                request.PhValue, request.FreeChlorineMgL, request.IsWaterClear, cancellationToken);
            if (trend.IsDeteriorating)
            {
                result.ThreeDayTrendDetected = true;
                result.ThreeDayTrendSummary = trend.Summary;
                result.TrendFindings = trend.Findings;
                result.TrendSuggestedChemicals = trend.SuggestedChemicals;
            }
        }

        return result;
    }
}
