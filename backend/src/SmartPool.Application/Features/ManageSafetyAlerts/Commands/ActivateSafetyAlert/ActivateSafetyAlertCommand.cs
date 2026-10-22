using MediatR;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Commands.ActivateSafetyAlert;

/// <summary>
/// Command kích hoạt cảnh báo cứu hộ thủ công từ nhân viên.
/// Broadcast real-time đến tất cả thiết bị đang kết nối qua SignalR.
/// </summary>
public sealed record ActivateSafetyAlertCommand : IRequest<ActivateSafetyAlertResult>
{
    /// <summary>Khu vực xảy ra sự cố: e.g. "Bể chính", "Bể trẻ em", "Khu thay đồ"</summary>
    public string Zone { get; init; } = string.Empty;

    /// <summary>Mức độ: "WARNING" (cảnh báo nhẹ) | "EMERGENCY" (khẩn cấp)</summary>
    public string Level { get; init; } = "EMERGENCY";

    /// <summary>Mô tả ngắn tình huống (tuỳ chọn)</summary>
    public string? Description { get; init; }

    /// <summary>Hình ảnh sự cố nếu có từ Camera AI giám sát</summary>
    public string? ImageIncidents { get; init; }

    /// <summary>UserId của người kích hoạt (inject từ JWT claim)</summary>
    public string TriggeredByUserId { get; init; } = string.Empty;

    /// <summary>Tên người kích hoạt (inject từ JWT claim)</summary>
    public string TriggeredByName { get; init; } = string.Empty;
}
