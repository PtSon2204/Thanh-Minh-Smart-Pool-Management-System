using SmartPool.Application.Features.ManageWaterQuality.Services;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class WaterQualityEvaluatorTests
{
    private readonly WaterQualityEvaluator _evaluator = new();

    [Theory(DisplayName = "UTC_WQ01 (N) - pH và Clo trong ngưỡng lý tưởng, nước trong -> Status WithinLimits")]
    [InlineData(7.20, 1.00, true)]
    [InlineData(7.50, 2.00, true)]
    [InlineData(7.80, 3.00, true)]
    [InlineData(7.40, 1.50, null)] // Không quan sát độ trong -> vẫn coi là đạt nếu chỉ số chuẩn
    public void UTC_WQ01_IdealValues_ReturnsWithinLimits(decimal ph, decimal chlorine, bool? isClear)
    {
        var result = _evaluator.Evaluate(ph, chlorine, isClear);

        Assert.Equal(WaterQualityConstants.StatusWithinLimits, result.Status);
        Assert.Contains("đạt chuẩn", result.Recommendation);
        Assert.Empty(result.SuggestedChemicals);
    }

    [Theory(DisplayName = "UTC_WQ02 (N) - pH hoặc Clo ở vùng Warning -> Status Warning và gợi ý hóa chất phù hợp")]
    [InlineData(7.00, 2.00, true, "Soda Ash")] // pH thấp vừa phải -> cần Soda Ash
    [InlineData(8.00, 2.00, true, "Axit Muriatic")] // pH cao vừa phải -> cần Axit Muriatic
    [InlineData(7.40, 0.80, true, "Clo (TCCA / Calcium Hypochlorite)")] // Clo hơi thấp -> châm Clo
    [InlineData(7.40, 3.50, true, "")] // Clo hơi cao -> giảm châm clo
    public void UTC_WQ02_WarningValues_ReturnsWarningAndChemicalSuggestion(
        decimal ph, decimal chlorine, bool? isClear, string expectedChemical)
    {
        var result = _evaluator.Evaluate(ph, chlorine, isClear);

        Assert.Equal(WaterQualityConstants.StatusWarning, result.Status);
        if (!string.IsNullOrEmpty(expectedChemical))
        {
            Assert.Contains(result.SuggestedChemicals, c => c.Contains(expectedChemical));
        }
    }

    [Fact(DisplayName = "UTC_WQ03 (N) - Nước bị đục (isWaterClear == false) -> Trạng thái Warning và gợi ý trợ lắng PAC")]
    public void UTC_WQ03_CloudyWater_ReturnsWarningAndPacChemical()
    {
        var result = _evaluator.Evaluate(7.40m, 2.00m, false);

        Assert.Equal(WaterQualityConstants.StatusWarning, result.Status);
        Assert.Contains(result.SuggestedChemicals, c => c.Contains("Trợ lắng PAC"));
        Assert.Contains("Nước bị đục", result.Recommendation);
    }

    [Theory(DisplayName = "UTC_WQ04 (N) - pH hoặc Clo vượt ngưỡng nguy hiểm -> Status Danger")]
    [InlineData(6.50, 2.00, true, "pH rất thấp")] // pH < 6.8
    [InlineData(8.50, 2.00, true, "pH quá cao")] // pH > 8.2
    [InlineData(7.40, 0.20, true, "Clo tự do rất thấp")] // Clo < 0.5
    [InlineData(7.40, 6.00, true, "Clo tự do quá cao")] // Clo > 5.0
    public void UTC_WQ04_DangerValues_ReturnsDangerAndSafetyWarning(
        decimal ph, decimal chlorine, bool? isClear, string expectedFindingKeyword)
    {
        var result = _evaluator.Evaluate(ph, chlorine, isClear);

        Assert.Equal(WaterQualityConstants.StatusDanger, result.Status);
        Assert.Contains(result.Findings, f => f.Contains(expectedFindingKeyword));
        Assert.Contains("CẢNH BÁO NGUY HIỂM", result.Recommendation);
    }

    [Fact(DisplayName = "UTC_WQ05 (N) - Clo quá cao (> 5.0) -> Gợi ý trung hòa bằng Sodium Thiosulfate")]
    public void UTC_WQ05_ExtremeChlorine_SuggestsSodiumThiosulfate()
    {
        var result = _evaluator.Evaluate(7.40m, 5.50m, true);

        Assert.Equal(WaterQualityConstants.StatusDanger, result.Status);
        Assert.Contains(result.SuggestedChemicals, c => c.Contains("Sodium Thiosulfate"));
    }
}
