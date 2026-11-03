using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetPoolDutySchedules;

public sealed class GetPoolDutySchedulesQuery : IRequest<PagedResponse<PoolDutyScheduleDto>>
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

