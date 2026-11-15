using MediatR;
using SmartPool.Application.Features.ManageEquipments.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipmentById;

public sealed record GetEquipmentByIdQuery(Guid Id) : IRequest<EquipmentListItem?>;
