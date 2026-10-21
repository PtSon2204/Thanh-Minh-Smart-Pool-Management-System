using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;
public sealed record MaintenanceSummary(int Total, int Pending, int InProgress, int Overdue);
