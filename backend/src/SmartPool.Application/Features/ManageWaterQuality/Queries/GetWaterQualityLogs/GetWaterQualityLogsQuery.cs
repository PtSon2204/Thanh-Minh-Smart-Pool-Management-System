using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetWaterQualityLogs;

public sealed class GetWaterQualityLogsQuery : IRequest<PagedResponse<WaterQualityLogDto>>
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public Guid? InspectedBy { get; set; }
}
