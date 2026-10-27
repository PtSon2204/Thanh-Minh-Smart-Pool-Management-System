using System.Text;

namespace SmartPool.Application.Features.ManageWaterQuality.Services;

public sealed class WaterQualityEvaluator : IWaterQualityEvaluator
{
    public WaterQualityEvaluationResult Evaluate(decimal phValue, decimal freeChlorineMgL, bool? isWaterClear)
    {
        var result = new WaterQualityEvaluationResult
        {
            EvaluationVersion = WaterQualityConstants.EvaluationVersion
        };

        bool isPhDanger = phValue < WaterQualityConstants.WarningPhMin || phValue > WaterQualityConstants.WarningPhMax;
        bool isPhWarning = !isPhDanger && (phValue < WaterQualityConstants.IdealPhMin || phValue > WaterQualityConstants.IdealPhMax);

        bool isChlorineDanger = freeChlorineMgL < WaterQualityConstants.WarningChlorineMin || freeChlorineMgL > WaterQualityConstants.WarningChlorineMax;
        bool isChlorineWarning = !isChlorineDanger && (freeChlorineMgL < WaterQualityConstants.IdealChlorineMin || freeChlorineMgL > WaterQualityConstants.IdealChlorineMax);

        bool isCloudy = isWaterClear == false;

        // Xác định Status tổng quan
        if (isPhDanger || isChlorineDanger)
        {
            result.Status = WaterQualityConstants.StatusDanger;
        }
        else if (isPhWarning || isChlorineWarning || isCloudy)
        {
            result.Status = WaterQualityConstants.StatusWarning;
        }
        else
        {
            result.Status = WaterQualityConstants.StatusWithinLimits;
        }

        // Đánh giá pH
        if (phValue < WaterQualityConstants.WarningPhMin)
        {
            result.Findings.Add($"pH rất thấp ({phValue:F2})");
            result.SuggestedChemicals.Add("Soda Ash (Na2CO3)");
        }
        else if (phValue < WaterQualityConstants.IdealPhMin)
        {
            result.Findings.Add($"pH thấp ({phValue:F2})");
            result.SuggestedChemicals.Add("Soda Ash (Na2CO3)");
        }
        else if (phValue > WaterQualityConstants.WarningPhMax)
        {
            result.Findings.Add($"pH quá cao ({phValue:F2})");
            result.SuggestedChemicals.Add("Axit Muriatic (HCl) / Sodium Bisulfate");
        }
        else if (phValue > WaterQualityConstants.IdealPhMax)
        {
            result.Findings.Add($"pH hơi cao ({phValue:F2})");
            result.SuggestedChemicals.Add("Axit Muriatic (HCl) / Sodium Bisulfate");
        }

        // Đánh giá Clo tự do
        if (freeChlorineMgL < WaterQualityConstants.WarningChlorineMin)
        {
            result.Findings.Add($"Clo tự do rất thấp ({freeChlorineMgL:F2} mg/L)");
            result.SuggestedChemicals.Add("Clo (TCCA / Calcium Hypochlorite)");
        }
        else if (freeChlorineMgL < WaterQualityConstants.IdealChlorineMin)
        {
            result.Findings.Add($"Clo tự do thấp ({freeChlorineMgL:F2} mg/L)");
            result.SuggestedChemicals.Add("Clo (TCCA / Calcium Hypochlorite)");
        }
        else if (freeChlorineMgL > WaterQualityConstants.WarningChlorineMax)
        {
            result.Findings.Add($"Clo tự do quá cao ({freeChlorineMgL:F2} mg/L)");
            result.SuggestedChemicals.Add("Sodium Thiosulfate (trung hòa clo)");
        }
        else if (freeChlorineMgL > WaterQualityConstants.IdealChlorineMax)
        {
            result.Findings.Add($"Clo tự do cao ({freeChlorineMgL:F2} mg/L)");
        }

        // Đánh giá độ trong
        if (isCloudy)
        {
            result.Findings.Add("Nước bị đục / cặn lơ lửng");
            result.SuggestedChemicals.Add("Trợ lắng PAC / Phèn nhôm");
        }

        // Tạo nội dung khuyến nghị chi tiết (ngắn gọn dưới 500 ký tự cho DB column limit)
        var recommendationSb = new StringBuilder();

        if (result.Status == WaterQualityConstants.StatusDanger)
        {
            recommendationSb.Append("CẢNH BÁO NGUY HIỂM: ");
            if (isChlorineDanger && freeChlorineMgL > WaterQualityConstants.WarningChlorineMax)
            {
                recommendationSb.Append("Tạm dừng bơi, tắt châm clo, bật sục khí hoặc châm Sodium Thiosulfate để hạ clo. ");
            }
            else if (isChlorineDanger && freeChlorineMgL < WaterQualityConstants.WarningChlorineMin)
            {
                recommendationSb.Append("Cần sốc clo hoặc châm bù Calcium Hypochlorite/TCCA để đảm bảo diệt khuẩn. ");
            }

            if (isPhDanger && phValue < WaterQualityConstants.WarningPhMin)
            {
                recommendationSb.Append("Châm Soda Ash (Na2CO3) hoặc xút để nâng pH lên 7.2-7.8. ");
            }
            else if (isPhDanger && phValue > WaterQualityConstants.WarningPhMax)
            {
                recommendationSb.Append("Châm Axit Muriatic (HCl loãng) hạ pH về ngưỡng chuẩn. ");
            }
        }
        else if (result.Status == WaterQualityConstants.StatusWarning)
        {
            recommendationSb.Append("CẢNH BÁO: ");
            if (phValue < WaterQualityConstants.IdealPhMin)
            {
                recommendationSb.Append("Châm Soda Ash (Na2CO3) để nâng pH. ");
            }
            else if (phValue > WaterQualityConstants.IdealPhMax)
            {
                recommendationSb.Append("Châm dung dịch axit loãng (HCl) để hạ pH. ");
            }

            if (freeChlorineMgL < WaterQualityConstants.IdealChlorineMin)
            {
                recommendationSb.Append("Châm bổ sung clo viên TCCA / bột Clo. ");
            }
            else if (freeChlorineMgL > WaterQualityConstants.IdealChlorineMax)
            {
                recommendationSb.Append("Giảm liều châm clo, bật sục khí để clo phân giải tự nhiên. ");
            }

            if (isCloudy)
            {
                recommendationSb.Append("Nước bị đục. Rửa ngược bình lọc cát (backwash), châm trợ lắng PAC và hút cặn đáy hồ. ");
            }
        }
        else
        {
            recommendationSb.Append($"Chất lượng nước đạt chuẩn (pH: {phValue:F2}, Clo: {freeChlorineMgL:F2} mg/L). Duy trì lọc tuần hoàn và đo định kỳ.");
        }

        var fullRec = recommendationSb.ToString().Trim();
        if (fullRec.Length > 490)
        {
            fullRec = fullRec[..487] + "...";
        }

        result.Recommendation = fullRec;
        return result;
    }
}
