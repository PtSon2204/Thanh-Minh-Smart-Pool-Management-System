using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;
public sealed record MaintenanceOptions(List<MaintenanceOption> Equipments, List<MaintenanceOption> Assignees);
