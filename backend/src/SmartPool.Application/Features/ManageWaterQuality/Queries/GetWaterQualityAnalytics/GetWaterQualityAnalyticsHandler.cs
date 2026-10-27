using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityAnalytics;

public sealed class GetWaterQualityAnalyticsHandler : IRequestHandler<GetWaterQualityAnalyticsQuery, WaterQualityAnalyticsResponse>
{
    private readonly IWaterQualityOperations _operations;

    public GetWaterQualityAnalyticsHandler(IWaterQualityOperations operations)
    {
        _operations = operations;
    }

    public async Task<WaterQualityAnalyticsResponse> Handle(GetWaterQualityAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _operations.GetWaterQualityAnalyticsAsync(request, cancellationToken);
    }
}
