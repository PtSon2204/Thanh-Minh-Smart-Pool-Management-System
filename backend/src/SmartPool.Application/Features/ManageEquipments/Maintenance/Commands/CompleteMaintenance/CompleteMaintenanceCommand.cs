using MediatR;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.CompleteMaintenance;

public sealed class CompleteMaintenanceCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public Guid PerformedBy { get; set; }
    public decimal Cost { get; set; }
    public string? VendorName { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool ReadyForUse { get; set; }
}
