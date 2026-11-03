using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;

public sealed class GetPoolDutySchedulesHandler : IRequestHandler<GetPoolDutySchedulesQuery, PagedResponse<PoolDutyScheduleDto>>
{
    private readonly IWaterQualityOperations _operations;

    public GetPoolDutySchedulesHandler(IWaterQualityOperations operations)
    {
        _operations = operations;
    }

    public async Task<PagedResponse<PoolDutyScheduleDto>> Handle(GetPoolDutySchedulesQuery request, CancellationToken cancellationToken)
    {
        return await _operations.GetPoolDutySchedulesAsync(request, cancellationToken);
    }
}

