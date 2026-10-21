using MediatR;

namespace SmartPool.Application.Features.ManageReports.Queries.GetOperationCostsStatistics;

public sealed record GetOperationCostsStatisticsQuery : IRequest<OperationCostsStatisticsResponse>
{
    /// <summary>
    /// Kỳ báo cáo chi phí: "month" (Tháng này), "quarter" (Quý này), "year" (Cả năm)
    /// </summary>
    public string? Period { get; init; } = "year";
}
