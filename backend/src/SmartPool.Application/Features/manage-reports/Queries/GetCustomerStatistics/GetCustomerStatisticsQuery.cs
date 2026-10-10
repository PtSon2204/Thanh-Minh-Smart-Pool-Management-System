using System;
using MediatR;

namespace SmartPool.Application.Features.ManageReports.Queries.GetCustomerStatistics;

public sealed record GetCustomerStatisticsQuery : IRequest<CustomerStatisticsResponse>
{
    /// <summary>
    /// Các khoảng thời gian: "today", "7days", "30days", "month", "custom"
    /// </summary>
    public string Period { get; init; } = "7days";
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}
