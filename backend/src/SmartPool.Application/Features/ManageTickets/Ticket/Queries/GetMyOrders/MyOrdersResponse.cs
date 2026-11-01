using SmartPool.Application.Features.ManageTickets.Ticket.DTOs;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Queries.GetMyOrders;

public sealed class MyOrdersResponse
{
    public List<MyOrderSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int UnpaidCount { get; set; }
}

public sealed class MyOrderSummaryResponse
{
    public int UnpaidCount { get; set; }
}

public sealed class MyOrderSummaryDto
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public List<OnlineOrderItemDto> Items { get; set; } = [];
}
