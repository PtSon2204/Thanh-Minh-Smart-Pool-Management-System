using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;

public sealed record GetDailyInspectionFormQuery : IRequest<IReadOnlyList<DailyInspectionFormItemDto>>;
