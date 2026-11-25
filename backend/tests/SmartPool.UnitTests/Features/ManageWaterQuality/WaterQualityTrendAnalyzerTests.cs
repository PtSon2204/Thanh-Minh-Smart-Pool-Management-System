using SmartPool.Application.Features.ManageWaterQuality.Services;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageWaterQuality;

public sealed class WaterQualityTrendAnalyzerTests
{
    [Fact(DisplayName = "UTC_WQ_TREND01 (N) - Chuỗi 3 ngày 7.7 -> 7.6 -> 7.4 (trong ngưỡng) + ngày 3 hơi đục -> Phát hiện đi xuống & đề xuất hóa chất")]
    public void AnalyzeThreeDayTrend_LeaderScenario_PhDropWithinRangeAndDay3Cloudy_DetectsAndRecommends()
    {
        var readings = new[]
        {
            new WaterQualityDailyReading(new DateOnly(2026, 11, 20), 7.70m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 21), 7.60m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 22), 7.40m, 2.00m, false),
        };

        var result = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(readings);

        Assert.True(result.IsDeteriorating);
        Assert.Contains(result.Findings, f => f.Contains("pH giảm liên tục 3 ngày"));
        Assert.Contains(result.Findings, f => f.Contains("hơi đục") || f.Contains("đục"));
        Assert.Contains("Soda Ash (Na2CO3)", result.SuggestedChemicals);
        Assert.Contains("Trợ lắng PAC / Phèn nhôm", result.SuggestedChemicals);
        Assert.InRange(result.DutyNotes.Length, 1, 500);

        var parsedChemicals = WaterQualityTrendAnalyzer.ParseSuggestedChemicalsFromNotes(result.DutyNotes);
        Assert.Contains("Soda Ash (Na2CO3)", parsedChemicals);
        Assert.Contains("Trợ lắng PAC / Phèn nhôm", parsedChemicals);
    }

    [Fact(DisplayName = "UTC_WQ_TREND02 (N) - Cả 3 ngày đều Trong vắt và trong khung chuẩn nhưng pH & Clo giảm dần 3 ngày -> Phát hiện thiếu hóa chất")]
    public void AnalyzeThreeDayTrend_WithinLimitsAndClearButMonotonicDrops_DetectsPhAndChlorine()
    {
        var readings = new[]
        {
            new WaterQualityDailyReading(new DateOnly(2026, 11, 20), 7.70m, 2.60m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 21), 7.50m, 2.10m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 22), 7.30m, 1.70m, true),
        };

        var result = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(readings);

        Assert.True(result.IsDeteriorating);
        Assert.Contains("Soda Ash (Na2CO3)", result.SuggestedChemicals);
        Assert.Contains("Clo (TCCA / Calcium Hypochlorite)", result.SuggestedChemicals);
    }

    [Fact(DisplayName = "UTC_WQ_TREND03 (N) - pH tăng liên tục 3 ngày (>= +0.20) -> Đề xuất Axit Muriatic / Sodium Bisulfate")]
    public void AnalyzeThreeDayTrend_PhRisingMonotonic_RecommendsAcid()
    {
        var readings = new[]
        {
            new WaterQualityDailyReading(new DateOnly(2026, 11, 20), 7.30m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 21), 7.45m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 22), 7.65m, 2.00m, true),
        };

        var result = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(readings);

        Assert.True(result.IsDeteriorating);
        Assert.Contains(result.SuggestedChemicals, c => c.Contains("Axit Muriatic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "UTC_WQ_TREND04 (N) - 3 ngày ổn định trong ngưỡng -> Không kích hoạt đề xuất lịch trực bể")]
    public void AnalyzeThreeDayTrend_StableWithinLimits_ReturnsNotDeteriorating()
    {
        var readings = new[]
        {
            new WaterQualityDailyReading(new DateOnly(2026, 11, 20), 7.40m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 21), 7.50m, 2.10m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 22), 7.42m, 2.05m, true),
        };

        var result = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(readings);

        Assert.False(result.IsDeteriorating);
        Assert.Empty(result.SuggestedChemicals);
        Assert.Equal(string.Empty, result.DutyNotes);
    }

    [Fact(DisplayName = "UTC_WQ_TREND05 (B) - Dữ liệu bị ngắt quãng ngày -> Không xét xu hướng 3 ngày liên tiếp")]
    public void AnalyzeThreeDayTrend_NonConsecutiveDates_ReturnsNotDeteriorating()
    {
        var readings = new[]
        {
            new WaterQualityDailyReading(new DateOnly(2026, 11, 19), 7.70m, 2.50m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 21), 7.50m, 2.00m, true),
            new WaterQualityDailyReading(new DateOnly(2026, 11, 22), 7.30m, 1.50m, false),
        };

        var result = WaterQualityTrendAnalyzer.AnalyzeThreeDayTrend(readings);

        Assert.False(result.IsDeteriorating);
    }
}
