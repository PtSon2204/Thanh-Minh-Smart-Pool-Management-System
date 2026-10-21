using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed record MaintenanceOption(Guid Value, string Label);
