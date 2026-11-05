using System;

namespace SmartPool.Domain.Entities;

public class PoolOperationalConfig
{
    public Guid Id { get; set; }

    public string PoolName { get; set; } = "Bể bơi Thành Minh";

    /// <summary>
    /// Sức chứa tối đa của bể bơi (Admin có thể cấu hình linh hoạt)
    /// </summary>
    public int MaxCapacity { get; set; } = 100;

    /// <summary>
    /// Ngưỡng phần trăm cảnh báo đông đúc (mặc định 80%, tức 80 khách)
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Giờ bắt đầu Ca sáng (Mặc định 06:00)
    /// </summary>
    public TimeOnly MorningStartTime { get; set; } = new TimeOnly(6, 0);

    /// <summary>
    /// Giờ kết thúc Ca sáng (Mặc định 11:30)
    /// </summary>
    public TimeOnly MorningEndTime { get; set; } = new TimeOnly(11, 30);

    /// <summary>
    /// Giờ bắt đầu Ca chiều (Mặc định 13:30)
    /// </summary>
    public TimeOnly AfternoonStartTime { get; set; } = new TimeOnly(13, 30);

    /// <summary>
    /// Giờ kết thúc Ca chiều (Mặc định 19:30)
    /// </summary>
    public TimeOnly AfternoonEndTime { get; set; } = new TimeOnly(19, 30);

    /// <summary>
    /// Trạng thái hoạt động: "Operating" (Đang mở cửa), "Maintenance" (Tạm dừng bảo trì), "Closed" (Đóng cửa)
    /// </summary>
    public string OperationalStatus { get; set; } = "Operating";

    /// <summary>
    /// Lý do tạm dừng hoạt động hoặc bảo trì
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>
    /// Cờ đánh dấu có phải do cảnh báo an toàn thiết bị kích hoạt hay không
    /// </summary>
    public bool IsAutoSuspended { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}
