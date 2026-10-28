using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Maintenance;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceHistory;

public sealed class GetMaintenanceHistoryQuery : IRequest<PagedResponse<MaintenanceHistoryItem>>
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public Guid? EquipmentId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
