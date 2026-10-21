using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageEquipments.Commands.SetEquipmentStatus;

public sealed class SetEquipmentStatusCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}
