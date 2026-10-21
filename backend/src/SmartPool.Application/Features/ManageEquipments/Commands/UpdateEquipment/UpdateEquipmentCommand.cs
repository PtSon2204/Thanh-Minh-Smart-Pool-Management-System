using FluentValidation;
using MediatR;
using System.Text.Json;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;

namespace SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;

public sealed class UpdateEquipmentCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public JsonElement? Metadata { get; set; }
}
