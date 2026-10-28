using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.ResumeMaintenance;

public sealed record ResumeMaintenanceCommand(Guid Id) : IRequest<Unit>;
