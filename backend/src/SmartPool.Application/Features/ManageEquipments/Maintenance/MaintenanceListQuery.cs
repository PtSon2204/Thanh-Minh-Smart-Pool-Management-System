using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceListQuery
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public Guid? EquipmentId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
