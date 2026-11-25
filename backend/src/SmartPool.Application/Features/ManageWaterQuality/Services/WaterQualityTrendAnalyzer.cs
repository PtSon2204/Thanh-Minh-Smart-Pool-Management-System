namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public sealed record WaterQualityDailyReading(
    DateOnly Date,
    decimal PhValue,
    decimal FreeChlorineMgL,
    bool? IsWaterClear);

public sealed class WaterQualityTrendAnalysisResult
{
    public bool IsDeteriorating { get; set; }
    public List<string> Findings { get; set; } = new();
    public List<string> SuggestedChemicals { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public string DutyNotes { get; set; } = string.Empty;
    public List<WaterQualityDailyReading> EvaluatedDays { get; set; } = new();
}

/// <summary>
/// Phân tích xu hướng chất lượng nước 3 ngày gần nhất theo chuẩn vận hành CDC MAHC / WHO / PHTA CPO.
/// Ngay cả khi chỉ số vẫn nằm trong khung an toàn, nếu biến thiên xấu liên tục qua 3 ngày
/// (ví dụ pH giảm dần 7.7 -> 7.6 -> 7.4, Clo suy giảm liên tục, hoặc nước chuyển đục),
/// hệ thống sẽ phát hiện nguyên nhân thiếu/dư và đề xuất hóa chất kèm lịch trực bể ngày hôm sau.
/// </summary>
public static class WaterQualityTrendAnalyzer
{
    private const string TrendPrefix = "Xu hướng 3 ngày";
    private const string ChemicalMarker = "| Hóa chất đề xuất:";

    public static WaterQualityTrendAnalysisResult AnalyzeThreeDayTrend(
        IReadOnlyList<WaterQualityDailyReading> orderedReadings)
    {
        var result = new WaterQualityTrendAnalysisResult();
        if (orderedReadings is null || orderedReadings.Count < WaterQualityConstants.TrendWindowDays)
        {
            return result;
        }

        var d0 = orderedReadings[^3];
        var d1 = orderedReadings[^2];
        var d2 = orderedReadings[^1];

        // Chỉ xét 3 ngày lịch liên tiếp D-2, D-1, D
        if (d1.Date != d0.Date.AddDays(1) || d2.Date != d1.Date.AddDays(1))
        {
            return result;
        }

        result.EvaluatedDays = [d0, d1, d2];

        var phDrop = d0.PhValue - d2.PhValue;
        var phRise = d2.PhValue - d0.PhValue;
        var phDroppingMonotonic = d0.PhValue > d1.PhValue && d1.PhValue > d2.PhValue;
        var phRisingMonotonic = d0.PhValue < d1.PhValue && d1.PhValue < d2.PhValue;

        var clDrop = d0.FreeChlorineMgL - d2.FreeChlorineMgL;
        var clDroppingMonotonic = d0.FreeChlorineMgL > d1.FreeChlorineMgL && d1.FreeChlorineMgL > d2.FreeChlorineMgL;

        var day3Cloudy = d2.IsWaterClear == false;
        var cloudyCount = new[] { d0, d1, d2 }.Count(x => x.IsWaterClear == false);
        var hasPhOrChlorineSlip = d2.PhValue < d1.PhValue
            || d2.PhValue < d0.PhValue
            || d2.FreeChlorineMgL < d1.FreeChlorineMgL
            || d2.FreeChlorineMgL < d0.FreeChlorineMgL
            || (phRisingMonotonic && phRise > 0m);

        // 1. pH giảm liên tục 3 ngày (xu hướng axit hóa / hụt độ kiềm TA)
        if (phDroppingMonotonic && (phDrop >= WaterQualityConstants.TrendPhMinCumulativeDelta || (day3Cloudy && phDrop > 0m)))
        {
            result.Findings.Add(
                $"pH giảm liên tục 3 ngày ({d0.PhValue:F2} → {d1.PhValue:F2} → {d2.PhValue:F2}, -{phDrop:F2})");
            AddChemical(result.SuggestedChemicals, "Soda Ash (Na2CO3)");
            AddChemical(result.SuggestedChemicals, "Sodium Bicarbonate (ổn định độ kiềm TA)");
        }
        // 2. pH tăng liên tục 3 ngày (giảm hiệu quả khử trùng Clo, nguy cơ kết tủa đục)
        else if (phRisingMonotonic && (phRise >= WaterQualityConstants.TrendPhMinCumulativeDelta || (day3Cloudy && phRise > 0m)))
        {
            result.Findings.Add(
                $"pH tăng liên tục 3 ngày ({d0.PhValue:F2} → {d1.PhValue:F2} → {d2.PhValue:F2}, +{phRise:F2})");
            AddChemical(result.SuggestedChemicals, "Axit Muriatic (HCl loãng) / Sodium Bisulfate");
        }

        // 3. Clo tự do suy giảm liên tục 3 ngày (tải hữu cơ / tiêu hao clo nhanh)
        if (clDroppingMonotonic
            && (clDrop >= WaterQualityConstants.TrendChlorineMinCumulativeDrop
                || (d2.FreeChlorineMgL <= WaterQualityConstants.TrendChlorineLowMargin
                    && clDrop >= WaterQualityConstants.TrendChlorineLowBufferDrop)
                || (day3Cloudy && clDrop > 0m)))
        {
            result.Findings.Add(
                $"Clo tự do giảm liên tục 3 ngày ({d0.FreeChlorineMgL:F2} → {d1.FreeChlorineMgL:F2} → {d2.FreeChlorineMgL:F2} mg/L, -{clDrop:F2})");
            AddChemical(result.SuggestedChemicals, "Clo (TCCA / Calcium Hypochlorite)");
        }

        // 4. Độ trong suy giảm ở ngày thứ 3 (hoặc đục nhiều ngày trong chuỗi 3 ngày)
        if (day3Cloudy && (cloudyCount >= 2 || hasPhOrChlorineSlip))
        {
            result.Findings.Add(cloudyCount >= 2
                ? $"Độ trong kém trong {cloudyCount}/3 ngày gần nhất (ngày {d2.Date:dd/MM} nước bị đục)"
                : $"Nước chuyển sang hơi đục / có cặn ở ngày thứ 3 ({d2.Date:dd/MM})");
            AddChemical(result.SuggestedChemicals, "Trợ lắng PAC / Phèn nhôm");
            AddChemical(result.SuggestedChemicals, "Rửa ngược bình lọc cát (Backwash)");
        }

        if (result.Findings.Count == 0)
        {
            return result;
        }

        result.IsDeteriorating = true;
        var findingsText = string.Join("; ", result.Findings);
        var chemicalsText = string.Join(", ", result.SuggestedChemicals);

        result.Summary = $"{TrendPrefix} ({d0.Date:dd/MM}–{d2.Date:dd/MM}): {findingsText}.";
        result.DutyNotes = FormatDutyNotes(d0.Date, d2.Date, findingsText, chemicalsText);
        return result;
    }

    public static List<string> ParseSuggestedChemicalsFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return [];
        }

        var markerIndex = notes.IndexOf(ChemicalMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return [];
        }

        var raw = notes[(markerIndex + ChemicalMarker.Length)..].Trim();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ExtractTrendSummaryFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return string.Empty;
        }

        var markerIndex = notes.IndexOf(ChemicalMarker, StringComparison.OrdinalIgnoreCase);
        return (markerIndex >= 0 ? notes[..markerIndex] : notes).Trim();
    }

    private static string FormatDutyNotes(DateOnly fromDate, DateOnly toDate, string findingsText, string chemicalsText)
    {
        var suffix = $" {ChemicalMarker} {chemicalsText}";
        var prefix = $"{TrendPrefix} ({fromDate:dd/MM}–{toDate:dd/MM}): ";
        const int maxTotalLength = 490;

        var availableFindingsLength = maxTotalLength - prefix.Length - suffix.Length;
        if (availableFindingsLength < 20)
        {
            var raw = $"{prefix}{findingsText}{suffix}";
            return raw.Length <= maxTotalLength ? raw : raw[..(maxTotalLength - 3)] + "...";
        }

        var trimmedFindings = findingsText.Length > availableFindingsLength
            ? findingsText[..(availableFindingsLength - 3)] + "..."
            : findingsText;

        return $"{prefix}{trimmedFindings}{suffix}";
    }

    private static void AddChemical(List<string> list, string chemical)
    {
        if (!list.Contains(chemical, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(chemical);
        }
    }
}
