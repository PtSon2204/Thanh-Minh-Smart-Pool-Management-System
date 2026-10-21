using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class CompleteMaintenanceRequest
{
    public decimal Cost { get; set; }
    public string? VendorName { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool ReadyForUse { get; set; }
}
