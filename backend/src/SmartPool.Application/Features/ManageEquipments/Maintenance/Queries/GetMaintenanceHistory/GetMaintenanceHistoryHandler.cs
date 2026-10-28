using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceHistory;

public sealed class GetMaintenanceHistoryHandler(IMaintenanceOperations operations)
    : IRequestHandler<GetMaintenanceHistoryQuery, PagedResponse<MaintenanceHistoryItem>>
{
    public Task<PagedResponse<MaintenanceHistoryItem>> Handle(GetMaintenanceHistoryQuery query, CancellationToken cancellationToken)
    {
        var listQuery = new MaintenanceListQuery
        {
            SearchTerm = query.SearchTerm,
            Status = query.Status,
            EquipmentId = query.EquipmentId,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
        return operations.GetHistoryAsync(listQuery, cancellationToken);
    }
}
