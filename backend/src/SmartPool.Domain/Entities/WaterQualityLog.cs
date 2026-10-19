namespace SmartPool.Domain.Entities;

public class WaterQualityLog
{
    public Guid Id { get; set; }

    // Thời điểm thực tế đo; dùng UTC, khác với thời điểm server lưu bản ghi.
    public DateTime InspectionTime { get; set; }

    public decimal PhValue { get; set; }

    // Clo tự do, đơn vị mg/L; không phải clorua hoặc clo tổng.
    public decimal FreeChlorineMgL { get; set; }

    // Null nghĩa là chưa quan sát hoặc thiết bị không cung cấp thông tin này.
    public bool? IsWaterClear { get; set; }

    // Manual hoặc Device. Backend xác định nguồn từ luồng tiếp nhận dữ liệu.
    public string Source { get; set; } = "Manual";

    // Bắt buộc với nguồn Manual; để null với nguồn Device.
    public Guid? InspectedBy { get; set; }

    // Bắt buộc với nguồn Device; nguồn Manual có thể ghi lại máy đã dùng để đo.
    public Guid? EquipmentId { get; set; }

    // Thiết bị giữ nguyên mã này khi gửi lại cùng một lần đo.
    public string? SourceEventId { get; set; }

    // Backend đánh giá; không mặc định nước đạt yêu cầu khi chưa có quy tắc.
    public string Status { get; set; } = "NotEvaluated";

    public string? Recommendation { get; set; }

    public string? EvaluationVersion { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? Inspector { get; set; }

    public virtual Equipment? Equipment { get; set; }
}
