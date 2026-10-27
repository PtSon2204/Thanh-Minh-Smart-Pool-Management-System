using MediatR;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;

public sealed class GetWaterQualityAnalyticsQuery : IRequest<WaterQualityAnalyticsResponse>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
