using System;

namespace SmartPool.Application.Features.Notifications.Models;

/// <summary>
/// DTO chứa thông tin chi tiết từng vé bơi trong đơn hàng phục vụ gửi email xác nhận.
/// </summary>
public sealed class TicketEmailItemDto
{
    public string TicketCode { get; set; } = string.Empty;
    public string TicketTypeName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int? RemainingEntries { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
