using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;

public sealed class CreateEquipmentCommand : IRequest<EquipmentListItem>
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public JsonElement? Metadata { get; set; }
    public CameraConnectionConfiguration? CameraConnection { get; set; }
}
