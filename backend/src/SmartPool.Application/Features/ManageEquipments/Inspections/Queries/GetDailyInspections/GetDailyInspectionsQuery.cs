using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspections;

public sealed class GetDailyInspectionsQuery : IRequest<PagedResponse<DailyInspectionDto>>
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? OverallStatus { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
