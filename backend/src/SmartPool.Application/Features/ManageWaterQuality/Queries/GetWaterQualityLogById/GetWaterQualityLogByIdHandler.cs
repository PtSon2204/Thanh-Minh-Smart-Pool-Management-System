using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogById;

public sealed class GetWaterQualityLogByIdHandler : IRequestHandler<GetWaterQualityLogByIdQuery, WaterQualityLogDto?>
{
    private readonly IWaterQualityOperations _operations;

    public GetWaterQualityLogByIdHandler(IWaterQualityOperations operations)
    {
        _operations = operations;
    }

    public async Task<WaterQualityLogDto?> Handle(GetWaterQualityLogByIdQuery request, CancellationToken cancellationToken)
    {
        return await _operations.GetWaterQualityLogByIdAsync(request.Id, cancellationToken);
    }
}
