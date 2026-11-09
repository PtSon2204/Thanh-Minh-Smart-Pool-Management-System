namespace SmartPool.Domain.Entities;

/// <summary>
/// Bảng đơn hàng (single-row): ngưỡng cảnh báo AI có thể chỉnh từ giao diện.
/// Giá trị tương đương <c>RiskOptions</c> (appsettings) nhưng ghi đè tại runtime
/// và được engine đọc live từng khung hình — đổi ngưỡng không làm mất state
/// theo dõi (episode, cửa sổ thời gian, cooldown).
/// </summary>
public class SafetyMonitoringThreshold
{
    /// <summary>Guid cố định duy nhất của bản ghi cấu hình (bảng single-row).</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000042");

    public Guid Id { get; set; } = SingletonId;
    // Giá trị mặc định đồng bộ với RiskOptions (appsettings) khi chưa có bản ghi.
    public double ConfidenceThreshold { get; set; } = 0.60;
    // Tỷ lệ frame "nguy hiểm" trong cửa sổ để phát cảnh báo / khẩn cấp / hồi phục.
    public double WarningRatio { get; set; } = 0.40;
    public double CriticalRatio { get; set; } = 0.70;
    public double RecoveryRatio { get; set; } = 0.20;
    // Số giây duy trì mức rồi mới chuyển / hồi.
    public double CriticalHoldSeconds { get; set; } = 3;
    public double RecoveryHoldSeconds { get; set; } = 5;
    // Khoãn sau hồi phục trước khi được phát cảnh báo mới cùng mức.
    public double CooldownSeconds { get; set; } = 15;
    // Cửa sổ quan sát (giây) và khoảng trống cho phép giữa các khung.
    public double WindowSeconds { get; set; } = 5;
    public double MaxObservationGapSeconds { get; set; } = 3;
    // Số quan sát tối thiểu trong cửa sổ trước khi phân loại.
    public int MinObservations { get; set; } = 3;

    // Phiên bản tăng dồn mỗi lần chỉnh — dùng hiển thị và dò cập nhật.
    public int Version { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
