using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceOptions;

public sealed class GetMaintenanceOptionsHandler(IMaintenanceOperations operations)
    : IRequestHandler<GetMaintenanceOptionsQuery, MaintenanceOptions>
{
    public Task<MaintenanceOptions> Handle(GetMaintenanceOptionsQuery query, CancellationToken cancellationToken)
        => operations.GetOptionsAsync(cancellationToken);
}
