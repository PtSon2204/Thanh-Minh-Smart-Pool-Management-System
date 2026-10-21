using SmartPool.Application.Features.ManageEquipments.Cameras;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.DTOs;

public sealed class EquipmentListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Metadata { get; set; }
    public string? Status { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }
    public CameraConnectionStatus? CameraConnection { get; set; }
    public string? CameraSource { get; set; }
}
