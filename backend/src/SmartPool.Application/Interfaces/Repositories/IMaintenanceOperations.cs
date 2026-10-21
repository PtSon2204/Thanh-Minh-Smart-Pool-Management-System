using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Maintenance;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IMaintenanceOperations
{
    Task<MaintenanceOptions> GetOptionsAsync(CancellationToken cancellationToken);
    Task<MaintenanceList> GetListAsync(MaintenanceListQuery query, CancellationToken cancellationToken);
    Task<MaintenanceItem> CreateAsync(MaintenanceRequest request, CancellationToken cancellationToken);
    Task<MaintenanceItem> UpdateAsync(Guid id, MaintenanceRequest request, CancellationToken cancellationToken);
    Task ChangeStateAsync(Guid id, string action, CancellationToken cancellationToken);
    Task CompleteAsync(Guid id, CompleteMaintenanceRequest request, Guid performedBy, CancellationToken cancellationToken);
    Task<PagedResponse<MaintenanceHistoryItem>> GetHistoryAsync(MaintenanceListQuery query, CancellationToken cancellationToken);
    Task SynchronizeAsync(CancellationToken cancellationToken);
}
