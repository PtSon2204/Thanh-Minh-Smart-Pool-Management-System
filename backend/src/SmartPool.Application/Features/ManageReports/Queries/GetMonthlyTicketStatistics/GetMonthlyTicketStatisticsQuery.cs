using MediatR;

namespace SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;

public sealed record GetMonthlyTicketStatisticsQuery : IRequest<MonthlyTicketStatisticsResponse>
{
    /// <summary>
    /// Bộ lọc danh sách cảnh báo thẻ: "all", "active", "expiring_soon", "expired"
    /// </summary>
    public string? Filter { get; init; } = "expiring_soon";
}
