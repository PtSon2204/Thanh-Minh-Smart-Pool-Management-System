using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;
public sealed record MaintenanceList(PagedResponse<MaintenanceItem> Page, MaintenanceSummary Summary);
