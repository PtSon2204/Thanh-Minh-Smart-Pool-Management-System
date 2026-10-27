namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public sealed class WaterQualityEvaluationResult
{
    public string Status { get; set; } = WaterQualityConstants.StatusNotEvaluated;
    public string Recommendation { get; set; } = string.Empty;
    public string EvaluationVersion { get; set; } = WaterQualityConstants.EvaluationVersion;
    public List<string> Findings { get; set; } = new();
    public List<string> SuggestedChemicals { get; set; } = new();
}
