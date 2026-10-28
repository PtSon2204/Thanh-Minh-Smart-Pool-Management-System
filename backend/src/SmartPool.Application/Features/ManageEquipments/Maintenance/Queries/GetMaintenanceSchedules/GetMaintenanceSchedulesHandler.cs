using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceSchedules;

public sealed class GetMaintenanceSchedulesHandler(IMaintenanceOperations operations)
    : IRequestHandler<GetMaintenanceSchedulesQuery, MaintenanceList>
{
    public Task<MaintenanceList> Handle(GetMaintenanceSchedulesQuery query, CancellationToken cancellationToken)
    {
        var listQuery = new MaintenanceListQuery
        {
            SearchTerm = query.SearchTerm,
            Status = query.Status,
            EquipmentId = query.EquipmentId,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
        return operations.GetListAsync(listQuery, cancellationToken);
    }
}
