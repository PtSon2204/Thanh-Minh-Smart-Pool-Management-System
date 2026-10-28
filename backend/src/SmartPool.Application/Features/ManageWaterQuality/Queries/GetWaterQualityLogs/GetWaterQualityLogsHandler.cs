using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;

public sealed class GetWaterQualityLogsHandler : IRequestHandler<GetWaterQualityLogsQuery, PagedResponse<WaterQualityLogDto>>
{
    private readonly IWaterQualityOperations _operations;

    public GetWaterQualityLogsHandler(IWaterQualityOperations operations)
    {
        _operations = operations;
    }

    public async Task<PagedResponse<WaterQualityLogDto>> Handle(GetWaterQualityLogsQuery request, CancellationToken cancellationToken)
    {
        return await _operations.GetWaterQualityLogsAsync(request, cancellationToken);
    }
}
