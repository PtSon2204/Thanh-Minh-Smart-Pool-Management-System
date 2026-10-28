using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceSchedules;

public sealed class GetMaintenanceSchedulesQuery : IRequest<MaintenanceList>
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public Guid? EquipmentId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
