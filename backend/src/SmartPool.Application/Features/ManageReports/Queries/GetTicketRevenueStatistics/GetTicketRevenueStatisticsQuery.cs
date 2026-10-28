using System;
using MediatR;

namespace SmartPool.Application.Features.ManageReports.Queries.GetTicketRevenueStatistics;

public sealed record GetTicketRevenueStatisticsQuery : IRequest<TicketRevenueStatisticsResponse>
{
    /// <summary>
    /// Các khoảng thời gian: "today", "7days", "30days", "month", "custom"
    /// </summary>
    public string Period { get; init; } = "7days";
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Phân loại vé: "all" (Tất cả), "VE_LUOT" (Vé lượt), "VE_THANG" (Vé tháng)
    /// </summary>
    public string? TicketCategory { get; init; } = "all";
}
