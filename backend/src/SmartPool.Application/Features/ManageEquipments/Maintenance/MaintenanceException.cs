using FluentValidation;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
