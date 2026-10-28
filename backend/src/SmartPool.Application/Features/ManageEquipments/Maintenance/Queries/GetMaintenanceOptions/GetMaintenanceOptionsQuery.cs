using MediatR;
using SmartPool.Application.Features.ManageEquipments.Maintenance;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceOptions;

public sealed record GetMaintenanceOptionsQuery : IRequest<MaintenanceOptions>;
