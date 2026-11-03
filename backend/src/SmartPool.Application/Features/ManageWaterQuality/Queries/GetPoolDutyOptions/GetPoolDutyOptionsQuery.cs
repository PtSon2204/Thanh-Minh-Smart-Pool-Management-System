using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutyOptions;

public sealed record GetPoolDutyOptionsQuery : IRequest<PoolDutyOptionsResponse>;

public sealed class GetPoolDutyOptionsHandler : IRequestHandler<GetPoolDutyOptionsQuery, PoolDutyOptionsResponse>
{
    private readonly IWaterQualityOperations _operations;

    public GetPoolDutyOptionsHandler(IWaterQualityOperations operations)
    {
        _operations = operations;
    }

    public async Task<PoolDutyOptionsResponse> Handle(GetPoolDutyOptionsQuery request, CancellationToken cancellationToken)
    {
        return await _operations.GetPoolDutyOptionsAsync(cancellationToken);
    }
}

