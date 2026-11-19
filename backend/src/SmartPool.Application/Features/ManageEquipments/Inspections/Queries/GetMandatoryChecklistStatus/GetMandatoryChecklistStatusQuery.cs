using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetMandatoryChecklistStatus;

public sealed record GetMandatoryChecklistStatusQuery : IRequest<MandatoryChecklistStatusDto>;
