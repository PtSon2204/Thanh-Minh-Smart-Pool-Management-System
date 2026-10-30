using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogById;

public sealed class GetWaterQualityLogByIdQuery : IRequest<WaterQualityLogDto?>
{
    public Guid Id { get; set; }

    public GetWaterQualityLogByIdQuery(Guid id)
    {
        Id = id;
    }

    public GetWaterQualityLogByIdQuery() { }
}
