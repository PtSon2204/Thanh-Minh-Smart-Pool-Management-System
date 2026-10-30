namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public interface IWaterQualityEvaluator
{
    WaterQualityEvaluationResult Evaluate(decimal phValue, decimal freeChlorineMgL, bool? isWaterClear);
}
