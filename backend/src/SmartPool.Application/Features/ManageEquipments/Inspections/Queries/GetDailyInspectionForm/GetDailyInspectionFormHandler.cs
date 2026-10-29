using MediatR;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspectionForm;

public sealed class GetDailyInspectionFormHandler(IRepository<Equipment> equipmentRepository)
    : IRequestHandler<GetDailyInspectionFormQuery, IReadOnlyList<DailyInspectionFormItemDto>>
{
    public async Task<IReadOnlyList<DailyInspectionFormItemDto>> Handle(
        GetDailyInspectionFormQuery request,
        CancellationToken cancellationToken)
    {
        var equipments = await equipmentRepository.FindAsync(
            x => x.IsDeleted != true,
            cancellationToken);

        return equipments
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new DailyInspectionFormItemDto
            {
                EquipmentId = x.Id,
                Name = x.Name,
                Category = x.Category,
                Location = x.Location,
                Status = x.Status ?? "Active"
            })
            .ToList();
    }
}
